// Unity built-in shader source. Copyright (c) 2016 Unity Technologies. MIT license (see license.txt)

Shader "UI/Default-Unified"
{
    Properties
    {
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        CGPROGRAM
		    #pragma enable_d3d11_debug_symbols

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #pragma multi_compile_local UNIFIELD_CLIPRECT

            sampler2D _StyleParamTex;
            sampler2D _FontParamTex;
            float4 _StyleParamTex_TexelSize;
            float4 _FontParamTex_TexelSize;
            //float4 _MainTex_ST;

            //sampler2D _MainTex;
            sampler2D _MainTex1;
			float4 _MainTex1_TexelSize;
            sampler2D _MainTex2;
			float4 _MainTex2_TexelSize;
            sampler2D _MainTex3;
			float4 _MainTex3_TexelSize;
            sampler2D _MainTex4;
			float4 _MainTex4_TexelSize;
            sampler2D _MainTex5;
			float4 _MainTex5_TexelSize;
            sampler2D _MainTex6;
			float4 _MainTex6_TexelSize;
            sampler2D _MainTex7;
			float4 _MainTex7_TexelSize;
            sampler2D _MainTex8;
			float4 _MainTex8_TexelSize;

            int _UIVertexColorAlwaysGammaSpace;

            float4 _ClipRectArray[48];
            float4 _SoftnessArray[48];

            half4 SampleTextureSlot(float2 uv, int index, float2 offset)
            {
                half4 result = half4(1,1,1,1);
                if (index < 4)
                {
                    if (index < 2)
                    {
                        if (index < 1)
                        {
                            result = tex2D(_MainTex1, uv + offset / _MainTex1_TexelSize.zw);
                        }
                        else
                        {
                            result = tex2D(_MainTex2, uv + offset / _MainTex2_TexelSize.zw);
                        }
                    }
                    else // index >= 2
                    {
                        if (index < 3)
                        {
                            result = tex2D(_MainTex3, uv + offset / _MainTex3_TexelSize.zw);
                        }
                        else
                        {
                            result = tex2D(_MainTex4, uv + offset / _MainTex4_TexelSize.zw);
                        }
                    }
                }
                else // index >= 4
                {
                    if (index < 6)
                    {
                        if (index < 5)
                        {
                            result = tex2D(_MainTex5, uv + offset / _MainTex5_TexelSize.zw);
                        }
                        else
                        {
                            result = tex2D(_MainTex6, uv + offset / _MainTex6_TexelSize.zw);
                        }
                    }
                    else // index >= 6
                    {
                        if (index < 7)
                        {
                            result = tex2D(_MainTex7, uv + offset / _MainTex7_TexelSize.zw);
                        }
                        else
                        {
                            result = tex2D(_MainTex8, uv + offset / _MainTex8_TexelSize.zw);
                        }
                    }
                }
                return result;
            }

			#include "SDF-Text.cginc"

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                float4 vPosition = UnityObjectToClipPos(v.vertex);
                OUT.vertex = vPosition;

                float2 pixelSize = vPosition.w;
                pixelSize /= float2(1, 1) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
              
                OUT.texcoord.xy = v.texcoord.xy;
                OUT.texcoord.z = v.texcoord2.z;
				OUT.texcoord.w = v.texcoord2.x;

				int type = round(OUT.texcoord.w + 0.01);
				if (type == 3) 
				{ 
                   OUT = Vert_SDFText(v, OUT);
				}
                else
                {
                    if (_UIVertexColorAlwaysGammaSpace)
                    {
                        if(!IsGammaSpace())
                        {
                            v.color.rgb = UIGammaToLinear(v.color.rgb);
                        }
                    }
                }

                OUT.color = v.color;
                OUT.sdfParam2.z = 0;
                if (v.texcoord2.y > 0.5) 
				{ 
					float4 clampedRect = clamp(_ClipRectArray[int(v.texcoord2.y)], -2e10, 2e10);
				    float2 maskSoftness = _SoftnessArray[int(v.texcoord2.y)].xy;
                    float2 maskUV = (v.vertex.xy - clampedRect.xy) / (clampedRect.zw - clampedRect.xy);
                    OUT.mask = float4(v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * half2(maskSoftness.x, maskSoftness.y) + abs(pixelSize.xy)));
				    OUT.clipRect = clampedRect;
                    OUT.sdfParam2.z = 1;
				}

                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                int index = round(IN.texcoord.z + 0.01);
				int type = round(IN.texcoord.w + 0.01);
                half4 color;
				float maskAlpha = 1;
                if (IN.sdfParam2.z > 0.5) 
				{ 
					half2 m = saturate((IN.clipRect.zw - IN.clipRect.xy - abs(IN.mask.xy)) * IN.mask.zw);
                    maskAlpha = m.x * m.y;
				}

				if (type == 1) 
				{ 
                    // Image
					color = IN.color * (SampleTextureSlot(IN.texcoord.xy, index, float2(0, 0)));
					color.a *= maskAlpha;
                    color.rgb *= color.a;

				}
				else if (type == 2) 
				{ 
                    // Text
					color = IN.color * (SampleTextureSlot(IN.texcoord.xy, index, float2(0, 0)) + fixed4(1, 1, 1, 0));
					color *= maskAlpha;
                    color.rgb *= color.a;
				}
                else if (type == 3) 
                {
					// SDF-Text
                    color = Frag_SDFText(IN, index);
					color *= maskAlpha;
                }
				
#ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
#endif

                return color;
            }
        ENDCG
        }
    }
}
