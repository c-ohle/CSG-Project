
cbuffer cbPerObject : register(b1) 
{
	matrix g_mWorld : packoffset(c0);
	float4 g_vDiffuse	: packoffset(c4);
};

struct VS_INPUT
{
	float4 P : POSITION;
	float3 N : NORMAL;
	float2 T : TEXCOORD0;
}; 

struct GS_P			
{ 
	float3 P : POS; 
}; 

GS_P main(VS_INPUT input) 
{ 
	GS_P p; p.P = mul(input.P, g_mWorld).xyz;
	return p;
}

