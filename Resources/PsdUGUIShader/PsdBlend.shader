Shader "Unlit/PsdBlend"
{
    Properties
    {
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
        
        [Header(Blend)]
		[Enum(PDNWrapper.LayerBlendMode)] _BlendMode ("    Mode", Int) = 0
        [Enum(UnityEngine.Rendering.BlendOp)] _BlendOp ("    Option", Int) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _BlendSrc ("    Src", Int) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _BlendDst ("    Dst", Int) = 10
    }
    SubShader
    {
        Tags {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "ignoreProjector"="True"
			"PreviewType" = "Plane"
			"CanUseSpriteAtlas" = "True"
        }

        Pass
        {
            ZWrite Off
            Lighting Off
            Cull Off
            Fog { Mode Off }
            
            BlendOp[_BlendOp]
			Blend[_BlendSrc][_BlendDst]
            
            CGPROGRAM
            
            #pragma vertex vert
            #pragma fragment frag
            #pragma fragmentoption ARB_precision_hint_fastest

            #include "UnityCG.cginc"

            struct MeshData
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
				float4 color : COLOR;
            };

            struct IterData
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
				float4 color : COLOR;
            };

            sampler2D _MainTex;
            int _BlendMode;

			static const int NORMAL = 0, MULTIPLY = 1, ADDITIVE = 2, COLOR_BURN = 3, COLOR_DODGE = 4, OVERLAY = 7, LIGHTEN = 10, DARKEN = 11, SCREEN = 12;

            IterData vert (MeshData meshData)
            {
                IterData iterData;
                iterData.vertex = UnityObjectToClipPos(meshData.vertex);
                iterData.uv = meshData.uv;
				iterData.color = meshData.color;
                return iterData;
            }

            fixed4 frag (IterData iterData) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, iterData.uv) * iterData.color;
				
				if (_BlendMode == MULTIPLY) {
					col.rgb *= col.a;
				}
                // else if (_BlendMode == COLOR_BURN) {
                //     col.rgb = col.rgb - (1.0 - col.rgb) * (1.0 - col.a) / col.a;
                // }
                // else if (_BlendMode == COLOR_DODGE) {
				// 	col.rgb = col.rgb + col.rgb * col.a / (1.0 - col.a);
				// }
                // else if (_BlendMode == OVERLAY) {
				// }
                else if (_BlendMode == LIGHTEN) {
					col.rgb = lerp(float3(0, 0, 0), col.rgb, col.a);
				}
                else if (_BlendMode == DARKEN) {
					col.rgb = lerp(float3(1, 1, 1), col.rgb, col.a);
				}
                else if (_BlendMode == SCREEN) {
					col.rgb *= col.a;
				}

                return col;
            }
            ENDCG
        }
    }
}
