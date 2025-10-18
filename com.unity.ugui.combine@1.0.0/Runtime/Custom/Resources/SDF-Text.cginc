
struct appdata_t
{
    float4 vertex   : POSITION;
    float4 color    : COLOR;
	float3 normal : NORMAL;
    float2 texcoord : TEXCOORD0;
	float4 texcoord1 : TEXCOORD1;   // SDF Text params
	float4 texcoord2 : TEXCOORD2;   // x:type(img/font) y:clipIndex z:param1 w:param2
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct v2f
{
    float4 vertex   : SV_POSITION;
    fixed4 color    : COLOR;
    float4 texcoord  : TEXCOORD0;
    float4 mask : TEXCOORD1;
#ifdef UNIFIELD_CLIPRECT
	float4 clipRect : TEXCOORD2;
#endif
    fixed4 faceColor : TEXCOORD3;
    fixed4 outlineColor : TEXCOORD4;
    fixed4 underlayColor : TEXCOORD5;
    float4 sdfParam1 : TEXCOORD6;       // scale, bias - outline, bias + outline, bias
    float4 sdfParam2 : TEXCOORD7;       // x:openOutline y:openUnderlay
    float4 sdfParam3 : TEXCOORD8;       // xy:underlayuv zw:undelayScale+bias
                
    UNITY_VERTEX_OUTPUT_STEREO
};


struct StyleParams
{
	float openOutline;
	float openUnderlay;

	float scaleRatioA;
	float scaleRatioB;
	float scaleRatioC;

    fixed4 faceColor;
    float faceDilate;

    fixed4 outlineColor;
    float outlineWidth;
    float outlineSoftness;

    fixed4 underlayColor;
    float2 underlayOffset;
    float underlaySoftness;
    float underlayDilate;
};

struct FontParams
{
	float gradientScale;
	float weightNormal;
	float weightBold;
};

float DecodeValue(float v)
{
	return v * 2 - 1;
}

FontParams SampleFontParams(float index)
{
	FontParams params;
	float4 params0 = tex2Dlod(_FontParamTex, float4(float2(index, 0) * _FontParamTex_TexelSize.xy, 0, 0));
	params.gradientScale = params0.x * 255.0;
	params.weightNormal = params0.y * 6.0 - 3.0;
	params.weightBold = params0.z * 6.0 - 3.0;
	return params;
}

StyleParams SampleStyleParams(float index)
{
    StyleParams params;
    float4 params0 = tex2Dlod(_StyleParamTex, float4(float2(index, 0) * _StyleParamTex_TexelSize.xy, 0, 0));
	float4 params4 = tex2Dlod(_StyleParamTex, float4(float2(index, 4) * _StyleParamTex_TexelSize.xy, 0, 0));

	bool openOutline       = params4.z > 0.5;
	bool openUnderlay      = params4.w > 0.5;

	params.openOutline = params4.z;
	params.openUnderlay = params4.w;
	params.faceDilate = DecodeValue(params0.y);
	params.scaleRatioA = params0.z;
	params.scaleRatioC = params0.w;
	params.outlineWidth = params4.x;
	params.outlineSoftness = params4.y;
    params.faceColor = tex2Dlod(_StyleParamTex, float4(float2(index, 1) * _StyleParamTex_TexelSize.xy, 0, 0));
#ifndef UNITY_COLORSPACE_GAMMA
	params.faceColor.rgb =	GammaToLinearSpace(params.faceColor.rgb);
#endif
	if (openOutline) 
	{ 
		params.outlineColor =   tex2Dlod(_StyleParamTex, float4(float2(index, 2) * _StyleParamTex_TexelSize.xy, 0, 0));
#ifndef UNITY_COLORSPACE_GAMMA
		params.outlineColor.rgb =	GammaToLinearSpace(params.outlineColor.rgb);
#endif
	}

	if (openUnderlay) 
	{ 
		params.underlayColor =  tex2Dlod(_StyleParamTex, float4(float2(index, 3) * _StyleParamTex_TexelSize.xy, 0, 0));
		float4 params5 =        tex2Dlod(_StyleParamTex, float4(float2(index, 5) * _StyleParamTex_TexelSize.xy, 0, 0));
		params.underlayOffset.x = DecodeValue(params5.x);
		params.underlayOffset.y = DecodeValue(params5.y);
		params.underlayDilate = DecodeValue(params5.z);
		params.underlaySoftness = params5.w;
#ifndef UNITY_COLORSPACE_GAMMA
		params.underlayColor.rgb =	GammaToLinearSpace(params.underlayColor.rgb);
#endif
	}
    return params;
}

v2f Vert_SDFText(in appdata_t input, in v2f output)
{
	v2f OUT = output;
	
	FontParams fontParams = SampleFontParams(input.texcoord1.w);
	StyleParams styleParams = SampleStyleParams(input.texcoord1.z);

	float bold = step(input.texcoord1.y, 0);
	float4 vert = input.vertex;
	float4 vPosition = UnityObjectToClipPos(vert);

	float2 pixelSize = vPosition.w;
	pixelSize /= float2(1, 1) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));

	float scale = rsqrt(dot(pixelSize, pixelSize));
	scale *= abs(input.texcoord1.y) * fontParams.gradientScale;
	if(UNITY_MATRIX_P[3][3] == 0) 
        scale = lerp(abs(scale) * (1 - 0.875), scale, abs(dot(UnityObjectToWorldNormal(input.normal.xyz), normalize(WorldSpaceViewDir(vert)))));

	float weight = lerp(fontParams.weightNormal, fontParams.weightBold, bold) / 4.0;
	weight = (weight + styleParams.faceDilate) * styleParams.scaleRatioA * 0.5;

	float layerScale = scale;

	scale /= 1 + (styleParams.outlineSoftness * styleParams.scaleRatioA * scale);
	float bias = (0.5 - weight) * scale - 0.5;
	float outline = styleParams.outlineWidth * styleParams.scaleRatioA * 0.5 * scale;

	float opacity = styleParams.openUnderlay > 0.5 ? 1.0 : input.color.a;

	fixed4 faceColor = fixed4(input.color.rgb, opacity) * styleParams.faceColor;
	faceColor.rgb *= faceColor.a;

	if (styleParams.openOutline > 0.5) 
	{ 
		fixed4 outlineColor = styleParams.outlineColor;
		outlineColor.a *= opacity;
		outlineColor.rgb *= outlineColor.a;
		outlineColor = lerp(faceColor, outlineColor, sqrt(min(1.0, (outline * 2))));
		OUT.outlineColor = outlineColor;
	}

	if (styleParams.openUnderlay > 0.5) 
	{ 
		layerScale /= 1 + ((styleParams.underlaySoftness * styleParams.scaleRatioC) * layerScale);
		float layerBias = (.5 - weight) * layerScale - .5 - ((styleParams.underlayDilate * styleParams.scaleRatioC) * .5 * layerScale);

		float x = -(styleParams.underlayOffset.x * styleParams.scaleRatioC) * fontParams.gradientScale;
		float y = -(styleParams.underlayOffset.y * styleParams.scaleRatioC) * fontParams.gradientScale;
		OUT.sdfParam3.xy = float2(x, y);
		OUT.sdfParam3.zw = half2(layerScale, layerBias);
		OUT.underlayColor = styleParams.underlayColor;
	}
			   
	OUT.faceColor = faceColor;
	OUT.sdfParam1 = half4(scale, bias - outline, bias + outline, bias);
	OUT.sdfParam2.x = styleParams.openOutline;
	OUT.sdfParam2.y = styleParams.openUnderlay;

	return OUT;
}

fixed4 Frag_SDFText(in v2f input, int index)
{
	half d = SampleTextureSlot(input.texcoord.xy, index, float2(0, 0)).a * input.sdfParam1.x;
	half4 c = input.faceColor * saturate(d - input.sdfParam1.w);

	if (input.sdfParam2.x > 0.5) 
	{ 
		c = lerp(input.outlineColor, input.faceColor, saturate(d - input.sdfParam1.z));
		c *= saturate(d - input.sdfParam1.y);
	}

	if (input.sdfParam2.y > 0.5) 
	{
		d = SampleTextureSlot(input.texcoord.xy, index, input.sdfParam3.xy).a * input.sdfParam3.z;
		c += float4(input.underlayColor.rgb * input.underlayColor.a, input.underlayColor.a) * saturate(d - input.sdfParam3.w) * (1 - c.a);
		c *= input.color.a;
	}
	return c;
}