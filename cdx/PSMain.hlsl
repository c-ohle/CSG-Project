cbuffer cbPsPerObject : register(b1) 
{
	float4 g_vDiffuse	: packoffset(c0);
};

struct PS_INPUT
{
	//float3 N : NORMAL;
	//float2 T : TEXCOORD0; 
};

float4 main(PS_INPUT Input) : SV_TARGET
{
	return g_vDiffuse;
}
