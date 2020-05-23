
cbuffer cbPerFrame : register(b0)
{
	matrix g_mViewProjection	: packoffset(c0);
	float3 g_fAmbient	: packoffset(c4);
	float4 g_vLightDir	: packoffset(c5);
};

struct PS_P			{ float4 P: SV_POSITION; };
struct GS_P			{ float3 P : POS; }; 

[maxvertexcount(18)]
void main(triangleadj GS_P a[6], inout TriangleStream<PS_P> stream)
{

	float3 N = cross(a[2].P - a[0].P, a[4].P - a[0].P);
	if (!(dot(N, g_vLightDir.xyz) > 0.0000001f)) return;

	PS_P pp[6];  float f = 1.0f / g_vLightDir.z; float w = g_vLightDir.w;
	pp[0].P = mul(float4(a[0].P, 1), g_mViewProjection); pp[3].P = mul(float4(a[0].P + g_vLightDir.xyz * ((w - a[0].P.z) * f), 1), g_mViewProjection);
	pp[1].P = mul(float4(a[2].P, 1), g_mViewProjection); pp[4].P = mul(float4(a[2].P + g_vLightDir.xyz * ((w - a[2].P.z) * f), 1), g_mViewProjection);
	pp[2].P = mul(float4(a[4].P, 1), g_mViewProjection); pp[5].P = mul(float4(a[4].P + g_vLightDir.xyz * ((w - a[4].P.z) * f), 1), g_mViewProjection);

	stream.Append(pp[0]); stream.Append(pp[1]); stream.Append(pp[2]); stream.RestartStrip();
	stream.Append(pp[3]); stream.Append(pp[5]); stream.Append(pp[4]); stream.RestartStrip();

	if (dot(cross(a[1].P - a[0].P, a[2].P - a[1].P), g_vLightDir.xyz) < 0.0000001f)
	{
		stream.Append(pp[0]); stream.Append(pp[3]);
		stream.Append(pp[1]); stream.Append(pp[4]); stream.RestartStrip();
	}
	if (dot(cross(a[3].P - a[2].P, a[4].P - a[3].P), g_vLightDir.xyz) < 0.0000001f)
	{
		stream.Append(pp[1]); stream.Append(pp[4]);
		stream.Append(pp[2]); stream.Append(pp[5]); stream.RestartStrip();
	}
	if (dot(cross(a[5].P - a[4].P, a[0].P - a[5].P), g_vLightDir.xyz) < 0.0000001f)
	{
		stream.Append(pp[2]); stream.Append(pp[5]);
		stream.Append(pp[0]); stream.Append(pp[3]); stream.RestartStrip();
	}

}
