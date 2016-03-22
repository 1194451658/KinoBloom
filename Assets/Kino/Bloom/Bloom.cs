﻿//
// Kino/Bloom v2 - Bloom filter for Unity
//
// Copyright (C) 2015, 2016 Keijiro Takahashi
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//
using UnityEngine;

namespace Kino
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("Kino Image Effects/Bloom")]
    public class Bloom : MonoBehaviour
    {
        #region Public Properties

        /// Prefilter threshold (gamma-encoded)
        /// Filters out pixels under this level of brightness.
        public float thresholdGamma {
            get { return Mathf.Max(_threshold, 0); }
            set { _threshold = value; }
        }

        /// Prefilter threshold (linearly-encoded)
        /// Filters out pixels under this level of brightness.
        public float thresholdLinear {
            get { return GammaToLinear(_threshold); }
            set { _threshold = LinearToGamma(value); }
        }

        [SerializeField]
        [Tooltip("Filters out pixels under this level of brightness.")]
        float _threshold = 0.5f;

        /// Soft-knee coefficient
        /// Makes transition between under/over-threshold gradual.
        public float softKnee {
            get { return _softKnee; }
            set { _softKnee = value; }
        }

        [SerializeField, Range(0, 1)]
        [Tooltip("Makes transition between under/over-threshold gradual.")]
        float _softKnee = 0.5f;

        /// Bloom radius
        /// Changes extent of veiling effects in a screen
        /// resolution-independent fashion.
        public float radius {
            get { return _radius; }
            set { _radius = value; }
        }

        [SerializeField, Range(0, 5)]
        [Tooltip("Changes extent of veiling effects\n" +
                 "in a screen resolution-independent fashion.")]
        float _radius = 2;

        /// Bloom intensity
        /// Blend factor of the result image.
        public float intensity {
            get { return Mathf.Max(_intensity, 0); }
            set { _intensity = value; }
        }

        [SerializeField]
        [Tooltip("Blend factor of the result image.")]
        float _intensity = 1.0f;

        /// High quality mode
        /// Controls filter quality and buffer resolution.
        public bool highQuality {
            get { return _highQuality; }
            set { _highQuality = value; }
        }

        [SerializeField]
        [Tooltip("Controls filter quality and buffer resolution.")]
        bool _highQuality = true;

        /// Anti-flicker filter
        /// Reduces flashing noise with an additional filter.
        [SerializeField]
        [Tooltip("Reduces flashing noise with an additional filter.")]
        bool _antiFlicker = false;

        public bool antiFlicker {
            get { return _antiFlicker; }
            set { _antiFlicker = value; }
        }

        #endregion

        #region Private Members

        [SerializeField, HideInInspector]
        Shader _shader;

        Material _material;

        float LinearToGamma(float x)
        {
        #if UNITY_5_3_OR_NEWER
            return Mathf.LinearToGammaSpace(x);
        #else
            if (x <= 0.0031308f)
                return 12.92f * x;
            else
                return 1.055f * Mathf.Pow(x, 1 / 2.4f) - 0.055f;
        #endif
        }

        float GammaToLinear(float x)
        {
        #if UNITY_5_3_OR_NEWER
            return Mathf.GammaToLinearSpace(x);
        #else
            if (x <= 0.04045f)
                return x / 12.92f;
            else
                return Mathf.Pow((x + 0.055f) / 1.055f, 2.4f);
        #endif
        }

        #endregion

        #region MonoBehaviour Functions

        void OnEnable()
        {
            var shader = _shader ? _shader : Shader.Find("Hidden/Kino/Bloom");
            _material = new Material(shader);
            _material.hideFlags = HideFlags.DontSave;
        }

        void OnDisable()
        {
            DestroyImmediate(_material);
        }

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            var useRGBM = Application.isMobilePlatform;
            var isGamma = QualitySettings.activeColorSpace == ColorSpace.Gamma;

            // source texture size
            var tw = source.width;
            var th = source.height;

            // halve the texture size for the low quality mode
            if (!_highQuality)
            {
                tw /= 2;
                th /= 2;
            }

            // blur buffer format
            var rtFormat = useRGBM ?
                RenderTextureFormat.Default : RenderTextureFormat.DefaultHDR;

            // determine the iteration count
            var logh = Mathf.Log(th, 2) + _radius - 6;
            var logh_i = (int)logh;
            var iteration = Mathf.Max(2, logh_i);

            // update the shader properties
            var lthresh = thresholdLinear;
            _material.SetFloat("_Threshold", lthresh);

            var knee = lthresh * _softKnee + 1e-5f;
            var curve = new Vector3(lthresh - knee, knee * 2, 0.25f / knee);
            _material.SetVector("_Curve", curve);

            var pfo = !_highQuality && _antiFlicker;
            _material.SetFloat("_PrefilterOffs", pfo ? -0.5f : 0.0f);

            _material.SetFloat("_SampleScale", 0.5f + logh - logh_i);
            _material.SetFloat("_Intensity", intensity);

            if (_highQuality)
                _material.EnableKeyword("HIGH_QUALITY");
            else
                _material.DisableKeyword("HIGH_QUALITY");

            if (_antiFlicker)
                _material.EnableKeyword("ANTI_FLICKER");
            else
                _material.DisableKeyword("ANTI_FLICKER");

            if (isGamma)
            {
                _material.DisableKeyword("LINEAR_COLOR");
                _material.EnableKeyword("GAMMA_COLOR");
            }
            else
            {
                _material.EnableKeyword("LINEAR_COLOR");
                _material.DisableKeyword("GAMMA_COLOR");
            }

            // allocate temporary buffers
            var rt1 = new RenderTexture[iteration + 1];
            var rt2 = new RenderTexture[iteration + 1];

            for (var i = 0; i < iteration + 1; i++)
            {
                rt1[i] = RenderTexture.GetTemporary(tw, th, 0, rtFormat);
                if (i > 0 && i < iteration)
                    rt2[i] = RenderTexture.GetTemporary(tw, th, 0, rtFormat);
                tw /= 2;
                th /= 2;
            }

            // apply the prefilter
            Graphics.Blit(source, rt1[0], _material, 0);

            // create a mip pyramid
            Graphics.Blit(rt1[0], rt1[1], _material, 1);

            for (var i = 1; i < iteration; i++)
                Graphics.Blit(rt1[i], rt1[i + 1], _material, 2);

            // blur and combine loop
            _material.SetTexture("_BaseTex", rt1[iteration - 1]);
            Graphics.Blit(rt1[iteration], rt2[iteration - 1], _material, 3);

            for (var i = iteration - 1; i > 1; i--)
            {
                _material.SetTexture("_BaseTex", rt1[i - 1]);
                Graphics.Blit(rt2[i],  rt2[i - 1], _material, 3);
            }

            // finish process
            _material.SetTexture("_BaseTex", source);
            Graphics.Blit(rt2[1], destination, _material, 4);

            // release the temporary buffers
            for (var i = 0; i < iteration + 1; i++)
            {
                RenderTexture.ReleaseTemporary(rt1[i]);
                RenderTexture.ReleaseTemporary(rt2[i]);
            }
        }

        #endregion
    }
}
