
cbuffer cbPerFrame : register(b0)
{
	matrix g_mViewProjection	: packoffset(c0);
	float3 g_fAmbient	: packoffset(c4);
	float4 g_vLightDir	: packoffset(c5);
};

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

struct VS_OUTPUT
{
	float3 N : NORMAL;
	float2 T : TEXCOORD0;
	float4 P : SV_POSITION;
};

VS_OUTPUT main(VS_INPUT Input)
{
	VS_OUTPUT Output;
	Output.P = mul(mul(Input.P, g_mWorld), g_mViewProjection);
	Output.N = max(0, dot(mul(Input.N, (float3x3)g_mWorld), g_vLightDir.xyz)) + g_fAmbient;
	Output.T = Input.T;
	return Output;
}
