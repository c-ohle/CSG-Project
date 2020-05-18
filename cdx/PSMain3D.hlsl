cbuffer cbPsPerObject : register(b1) 
{
	float4 g_vDiffuse	: packoffset(c0);
};

struct PS_INPUT
{
	float3 N : NORMAL;
	float2 T : TEXCOORD0; 
};

float4 main(PS_INPUT Input) : SV_TARGET
{ 
	return float4(g_vDiffuse.xyz * Input.N, g_vDiffuse.w);
	//float4 vDiffuse = float4(0,1,0,1);//g_txDiffuse.Sample(g_samLinear, Input.vTexcoord);
	//float fLighting = saturate(dot(g_vLightDir, Input.vNormal));
	//fLighting = max(fLighting, g_fAmbient);
	//return vDiffuse * fLighting;
}
