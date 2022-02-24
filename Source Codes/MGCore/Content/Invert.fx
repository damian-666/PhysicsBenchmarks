sampler s0;


float4 PixelShaderFunction(float4 pos : SV_POSITION, float4 color1 : COLOR0, float2 coords : TEXCOORD0) : SV_TARGET0
{
    float4 color = tex2D(s0, coords);
	
	if (!any(color)) return color;
	
	color.rgb = 1 - color.rgb;

    return color;
}

technique Technique1
{
    pass Pass1
    {
        PixelShader = compile ps_4_0 PixelShaderFunction();
    }
}