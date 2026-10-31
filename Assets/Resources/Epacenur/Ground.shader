// The overworld's ground: grass, earth, rock and snow blended smoothly between the corners of its tiles. Each
// corner carries the grass's colour (vertex colour rgb), how much snow (vertex colour alpha), and how much earth
// and rock (second uv). Each texture's own brightness pushes it through at the edges, so the edges follow its
// detail. Only Unity's standard inputs are used (uv_, uv2_, COLOR), so nothing's lost on the way.
Shader "Epacenur/Ground"
{
    Properties
    {
        _GrassTex ("Grass", 2D) = "white" {}
        _EarthTex ("Earth", 2D) = "white" {}
        _RockTex ("Rock", 2D) = "white" {}
        _SnowTex ("Snow", 2D) = "white" {}
        _EarthTint ("Earth tint", Color) = (0.5,0.4,0.28,1)
        _RockTint ("Rock tint", Color) = (0.55,0.54,0.5,1)
        _SnowTint ("Snow tint", Color) = (1,1,1,1)
        _Detail ("Detail", Float) = 0.5
        _Edge ("Edge", Float) = 0.2
        _Glossiness ("Smoothness", Range(0,1)) = 0.05
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _GrassTex, _EarthTex, _RockTex, _SnowTex;
        fixed4 _EarthTint, _RockTint, _SnowTint;
        float _Detail, _Edge;
        half _Glossiness;

        struct Input
        {
            float2 uv_GrassTex;    // the texture coordinates, repeated by _GrassTex's tiling (all four textures share them)
            float2 uv2_EarthTex;   // how much earth (x) and rock (y): _EarthTex keeps the default tiling, so these come through as they are
            float4 color : COLOR;  // the grass's colour, and how much snow (alpha)
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 uv = IN.uv_GrassTex;
            fixed3 g = tex2D(_GrassTex, uv).rgb, e = tex2D(_EarthTex, uv).rgb, r = tex2D(_RockTex, uv).rgb, s = tex2D(_SnowTex, uv).rgb;
            float we = IN.uv2_EarthTex.x, wr = IN.uv2_EarthTex.y, ws = IN.color.a, wg = saturate(1 - we - wr - ws);
            float4 h = float4(wg + g.r * _Detail, we + e.r * _Detail, wr + r.r * _Detail, ws + s.r * _Detail);
            float top = max(max(h.x, h.y), max(h.z, h.w)) - _Edge;
            float4 b = max(h - top, 0);
            b /= (b.x + b.y + b.z + b.w);
            o.Albedo = b.x * g * IN.color.rgb + b.y * e * _EarthTint.rgb + b.z * r * _RockTint.rgb + b.w * s * _SnowTint.rgb;
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
