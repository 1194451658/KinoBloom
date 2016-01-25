﻿//
// KinoBloom - Bloom effect
//
// Copyright (C) 2015 Keijiro Takahashi
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
Shader "Hidden/Kino/Bloom"
{
    Properties
    {
        _MainTex("", 2D) = "" {}
        _BaseTex("", 2D) = "" {}
    }

    CGINCLUDE

    #include "UnityCG.cginc"

    #pragma multi_compile _ PREFILTER_MEDIAN

    sampler2D _MainTex;
    sampler2D _BaseTex;

    float2 _MainTex_TexelSize;
    float2 _BaseTex_TexelSize;

    float _SampleScale;
    half _Prefilter;
    half _Intensity;

    half3 median(half3 a, half3 b, half3 c)
    {
        return a + b + c - min(min(a, b), c) - max(max(a, b), c);
    }

    half4 frag_prefilter(v2f_img i) : SV_Target
    {
#if PREFILTER_MEDIAN
        float3 d = _MainTex_TexelSize.xyx * float3(1, 1, 0);

        half4 s0 = tex2D(_MainTex, i.uv);
        half3 s1 = tex2D(_MainTex, i.uv - d.xz).rgb;
        half3 s2 = tex2D(_MainTex, i.uv + d.xz).rgb;
        half3 s3 = tex2D(_MainTex, i.uv - d.zy).rgb;
        half3 s4 = tex2D(_MainTex, i.uv + d.zy).rgb;

        half3 m = median(median(s0.rgb, s1, s2), s3, s4);
#else
        half4 s0 = tex2D(_MainTex, i.uv);
        half3 m = s0.rgb;
#endif

        m *= smoothstep(0, _Prefilter, Luminance(m));

        return half4(m, s0.a);
    }

    half4 frag_box_reduce(v2f_img i) : SV_Target
    {
        float4 d = _MainTex_TexelSize.xyxy * float4(-1, -1, +1, +1);

        float3 s;
        s  = tex2D(_MainTex, i.uv + d.xy).rgb;
        s += tex2D(_MainTex, i.uv + d.zy).rgb;
        s += tex2D(_MainTex, i.uv + d.xw).rgb;
        s += tex2D(_MainTex, i.uv + d.zw).rgb;

        return half4(s * 0.25, 0);
    }

    half4 frag_tent_expand(v2f_img i) : SV_Target
    {
        float4 d = _MainTex_TexelSize.xyxy * float4(1, 1, -1, 0) * _SampleScale;

        float4 base = tex2D(_BaseTex, i.uv);

        float3 s;
        s  = tex2D(_MainTex, i.uv - d.xy).rgb;
        s += tex2D(_MainTex, i.uv - d.wy).rgb * 2;
        s += tex2D(_MainTex, i.uv - d.zy).rgb;

        s += tex2D(_MainTex, i.uv + d.zw).rgb * 2;
        s += tex2D(_MainTex, i.uv       ).rgb * 4;
        s += tex2D(_MainTex, i.uv + d.xw).rgb * 2;

        s += tex2D(_MainTex, i.uv + d.zy).rgb;
        s += tex2D(_MainTex, i.uv + d.wy).rgb * 2;
        s += tex2D(_MainTex, i.uv + d.xy).rgb;

        return half4(base.rgb + s * (1.0 / 16), base.a);
    }

    half4 frag_combine(v2f_img i) : SV_Target
    {
        half4 base = tex2D(_BaseTex, i.uv);
        half3 blur = tex2D(_MainTex, i.uv).rgb;
        return half4(base.rgb + blur * _Intensity, base.a);
    }

    ENDCG
    SubShader
    {
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag_prefilter
            #pragma target 3.0
            ENDCG
        }
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag_box_reduce
            #pragma target 3.0
            ENDCG
        }
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag_tent_expand
            #pragma target 3.0
            ENDCG
        }
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag_combine
            #pragma target 3.0
            ENDCG
        }
    }
}
