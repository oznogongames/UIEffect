using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// 静的なスクリーンブラーを表示します.
/// ポストエフェクト等によるリアルタイムスクリーンブラーとは異なり、ある時点でのスクリーンショットに対するブラーのみを提供します.
/// 1. ブラー処理用のCameraが不要です.
/// 2. ブラーは常時実行されません. テクスチャ更新を実行したあと1度だけ実行されます.
/// 3. 縮小バッファを利用することで、メモリサイズを小さく抑えます.
/// 4. スクリーン | ブラー | ダイアログ1 | ブラー | ダイアログ2 ... のように、重ねて表示できます.
/// 5. 激しい動きのあるオブジェクトがスクリーン上にある場合、ブラーテクスチャにズレが発生し得ます.
/// </summary>
public class UIEffectCapturedImage : RawImage
{
	/// <summary>
	/// Desampling rate.
	/// </summary>
	public enum DesamplingRate
	{
		None = 0,
		x1 = 1,
		x2 = 2,
		x4 = 4,
		x8 = 8,
	}


	/// <summary>
	/// Tone effect level between 0(no effect) and 1(complete effect).
	/// </summary>
	public float toneLevel { get { return m_ToneLevel; } set { m_ToneLevel = Mathf.Clamp(value, 0, 1); } }

	[SerializeField]
	[Range(0, 1)]
	float m_ToneLevel = 1;

	/// <summary>
	/// How far is the blurring from the graphic.
	/// </summary>
	public float blur { get { return m_Blur; } set { m_Blur = Mathf.Clamp(value, 0, 4); } }

	[SerializeField]
	[Range(0, 4)]
	float m_Blur = 0;

	/// <summary>
	/// Tone effect mode.
	/// </summary>
	public UIEffect.ToneMode toneMode { get { return m_ToneMode; } set { m_ToneMode = value; } }

	[SerializeField]
	UIEffect.ToneMode m_ToneMode;

	/// <summary>
	/// Color effect mode.
	/// </summary>
	public UIEffect.ColorMode colorMode { get { return m_ColorMode; } set { m_ColorMode = value; } }

	[SerializeField]
	UIEffect.ColorMode m_ColorMode;

	/// <summary>
	/// Blur effect mode.
	/// </summary>
	public UIEffect.BlurMode blurMode { get { return m_BlurMode; } set { m_BlurMode = value; } }

	[SerializeField]
	UIEffect.BlurMode m_BlurMode;

	/// <summary>
	/// Color for the color effect.
	/// </summary>
	public Color effectColor { get { return m_EffectColor; } set { m_EffectColor = value; } }

	[SerializeField]
	Color m_EffectColor = Color.white;

	/// <summary>
	/// Effect shader.
	/// </summary>
	public virtual Shader shader { get { if (m_Shader == null) m_Shader = Shader.Find("UI/Hidden/UIEffectCapturedImage"); return m_Shader; } }

	[SerializeField]
	Shader m_Shader;

	/// <summary>
	/// Desampling rate of the generated RenderTexture.
	/// </summary>
	public DesamplingRate desamplingRate { get { return m_DesamplingRate; } set { m_DesamplingRate = value; } }

	[SerializeField]
	DesamplingRate m_DesamplingRate;

	/// <summary>
	/// Desampling rate of reduction buffer to apply effect.
	/// </summary>
	public DesamplingRate reductionRate { get { return m_ReductionRate; } set { m_ReductionRate = value; } }

	[SerializeField]
	DesamplingRate m_ReductionRate;

	/// <summary>
	/// FilterMode for capture.
	/// </summary>
	public FilterMode filterMode { get { return m_FilterMode; } set { m_FilterMode = value; } }

	[SerializeField]
	FilterMode m_FilterMode = FilterMode.Bilinear;


	public void GetDesamplingSize(DesamplingRate rate, out int w, out int h)
	{
		var camera = canvas.worldCamera ?? Camera.main;
		h = camera.pixelHeight;
		w = camera.pixelWidth;
		if (rate != DesamplingRate.None)
		{
			h = Mathf.ClosestPowerOfTwo(h / (int)rate);
			w = Mathf.ClosestPowerOfTwo(w / (int)rate);
		}
	}


	/// <summary>
	/// ブラーテクスチャを更新します
	/// </summary>
	public void UpdateTexture()
	{
		// レンダリングカメラはCanvasのカメラを利用します.
		_camera = canvas.worldCamera ?? Camera.main;

		// staticなオブジェクトをキャッシュ.
		if (s_CopyId == 0)
		{
			s_CopyId = Shader.PropertyToID("_ScreenCopy");
			s_EffectId = Shader.PropertyToID("_Effect");
		}

		// 出力先RT生成.
		int w, h;
		GetDesamplingSize(m_DesamplingRate, out w, out h);
		if (_rt && (_rt.width != w || _rt.height != h))
		{
			_rtToRelease = _rt;
			_rt = null;
		}

		if (_rt == null)
		{
			// 出力先RT生成
			_rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
			_rt.filterMode = m_FilterMode;
			_rt.useMipMap = false;
			_rt.wrapMode = TextureWrapMode.Clamp;
			_rt.hideFlags = HideFlags.HideAndDontSave;
		}

		// コマンドバッファ生成.
		if (_buffer == null)
		{
			// 出力先RTのID生成
			var rtId = new RenderTargetIdentifier(_rt);

			// Material for effect.
			var mat = UIEffect.GetSharedMaterial(shader, toneMode, colorMode, blurMode, false);

			// コマンドバッファ生成
			_buffer = new CommandBuffer();
			_buffer.name = mat.name;
			_rt.name = mat.name;

			// テンポラリRTにスクリーンコピー
			_buffer.GetTemporaryRT(s_CopyId, -1, -1, 0, FilterMode.Bilinear);
			_buffer.Blit(BuiltinRenderTextureType.CurrentActive, s_CopyId);

			// Set properties.
			_buffer.SetGlobalVector("_EffectFactor", new Vector4(toneLevel, blur / w));
			_buffer.SetGlobalVector("_ColorFactor", new Vector4(effectColor.r, effectColor.g, effectColor.b, effectColor.a));

			GetDesamplingSize(m_ReductionRate, out w, out h);
			_buffer.GetTemporaryRT(s_EffectId, w, h, 0, FilterMode.Bilinear);
			_buffer.Blit(s_CopyId, s_EffectId, mat);
			_buffer.Blit(s_EffectId, rtId);

			_buffer.ReleaseTemporaryRT(s_EffectId);
			_buffer.ReleaseTemporaryRT(s_CopyId);
		}

		// コマンドバッファをカメラに追加します.
		_camera.AddCommandBuffer(kCameraEvent, _buffer);

		// 1フレーム後の処理を追加します.
		// コルーチン呼び出しの移譲先として、CanvasScalerを取得します.
		// コルーチン呼び出しを以上することで、このオブジェクトが非アクティブな状態でもブラーテクスチャが更新できます.
#if UNITY_5_4_OR_NEWER
		canvas.rootCanvas.GetComponent<CanvasScaler>().StartCoroutine(_CoUpdateTextureOnNextFrame());
#else
		canvas.GetComponentInParent<CanvasScaler>().StartCoroutine(_CoUpdateTextureOnNextFrame());
#endif
	}

	public void ReleaseTexture()
	{
		_Release(true);
	}


	/// <summary>
	/// This function is called when the MonoBehaviour will be destroyed.
	/// </summary>
	protected override void OnDestroy()
	{
		_Release(true);
		base.OnDestroy();
	}

	/// <summary>
	/// Callback function when a UI element needs to generate vertices.
	/// </summary>
	protected override void OnPopulateMesh(VertexHelper vh)
	{
		// 非表示状態ならば、vhをクリアし、オーバードローを抑えます.
		if (texture == null || effectColor.a < 1 / 255f || canvasRenderer.GetAlpha() < 1 / 255f)
			vh.Clear();
		else
			base.OnPopulateMesh(vh);
	}

	const CameraEvent kCameraEvent = CameraEvent.AfterEverything;
	Camera _camera;
	RenderTexture _rt;
	RenderTexture _rtToRelease;
	CommandBuffer _buffer;

	//static Material s_MaterialBlur;
	static int s_CopyId;
	static int s_EffectId;

	void _Release(bool releaseRT)
	{
		// 生成したオブジェクトを解放します.
		if(releaseRT)
		{
			texture = null;

			if (_rt != null)
			{
				_rt.Release();
				_rt = null;
			}
		}

		if (_buffer != null)
		{
			if (_camera != null)
				_camera.RemoveCommandBuffer(kCameraEvent, _buffer);
			_buffer.Release();
			_buffer = null;
		}

		if (_rtToRelease)
		{
			_rtToRelease.Release();
			_rtToRelease = null;
		}
	}

	/// <summary>
	/// 次フレームでアクションを実行します.
	/// </summary>
	IEnumerator _CoUpdateTextureOnNextFrame()
	{
		yield return new WaitForEndOfFrame();

		_Release(false);
		texture = _rt;
		//SetMaterialDirty();
	}
}
