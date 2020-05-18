
cbuffer cbPerFrame : register(b0)
{
	matrix g_mViewProjection	: packoffset(c0);
	float3 g_fAmbient	: packoffset(c4);
	float4 g_vLightDir	: packoffset(c5);
};

struct PS_P { float4 P: SV_POSITION; };
struct GS_P { float3 P : POS; }; 

void outline3d(float3 A, GS_P v1, GS_P v2, GS_P a, inout LineStream<PS_P> stream)
{ 
	float3 N = cross(v2.P - a.P, v1.P - a.P);
	if(dot(N, g_vLightDir.xyz - a.P) >= 0) 
	{
		if(v1.P.x > v2.P.x) return;
		float3 D = normalize(cross(v2.P - a.P, v1.P - a.P)) - A;
		if(D.x * D.x + D.y * D.y + D.z * D.z < 0.3f) return;
	}
	PS_P p;
	p.P = mul(float4(v1.P, 1), g_mViewProjection); stream.Append(p);
	p.P = mul(float4(v2.P, 1), g_mViewProjection); stream.Append(p);
	stream.RestartStrip();
} 

[maxvertexcount(6)]
void main(triangleadj GS_P In[6], inout LineStream<PS_P> stream)
{
	float3 N = cross(In[2].P - In[0].P, In[4].P - In[0].P);
	if(dot(N, g_vLightDir.xyz - In[0].P) <= 0 ) return;
	N = normalize(N);
	outline3d(N, In[0], In[2], In[1], stream);
	outline3d(N, In[2], In[4], In[3], stream);
	outline3d(N, In[4], In[0], In[5], stream);
	stream.RestartStrip();
}