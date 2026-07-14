Shader "Custom/BuiltInBoxSelection"
{
    Properties
    {
        // 你的木箱贴图
        _MainTex ("Base (RGB)", 2D) = "white" {}
        _Color ("Main Color", Color) = (1,1,1,1)
        
        // 描边设置
        [HDR] _RimColor ("Outline Color", Color) = (1,0,0,1) // 默认红色
        _RimPower ("Outline Width", Range(0.5, 8.0)) = 3.0   // 越小越宽
        _IsSelected ("Is Selected", Range(0.0, 1.0)) = 0.0   // 0=关闭, 1=开启
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // 使用简单的 Lambert 光照模型，接近 Mobile/Diffuse 的效果
        #pragma surface surf Lambert

        sampler2D _MainTex;
        fixed4 _Color;
        fixed4 _RimColor;
        float _RimPower;
        float _IsSelected;

        struct Input
        {
            float2 uv_MainTex;
            float3 viewDir; // 获取视角方向
        };

        void surf (Input IN, inout SurfaceOutput o)
        {
            // 1. 基础纹理颜色
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Alpha = c.a;

            // 2. 边缘光计算 (Fresnel)
            // 如果 _IsSelected 大于 0.5，才显示描边
            if (_IsSelected > 0.5)
            {
                // 计算视角和法线的夹角 (0=边缘, 1=中心)
                half rim = 1.0 - saturate(dot (normalize(IN.viewDir), o.Normal));
                
                // 指数运算控制边缘宽度，叠加到自发光 (Emission)
                o.Emission = _RimColor.rgb * pow (rim, _RimPower);
            }
            else
            {
                o.Emission = 0;
            }
        }
        ENDCG
    }
    
    // 如果显卡太烂不支持 Shader，回退到 Mobile/Diffuse
    FallBack "Mobile/Diffuse"
}