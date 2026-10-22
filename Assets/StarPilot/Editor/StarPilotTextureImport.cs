using UnityEditor;
using UnityEngine;

namespace StarPilot.Editor
{
    public sealed class StarPilotTextureImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if(!assetPath.Contains("StarPilot/Resources/StarPilot/Textures/"))return;
            var t=(TextureImporter)assetImporter;t.mipmapEnabled=true;t.filterMode=FilterMode.Trilinear;t.anisoLevel=8;
            t.wrapMode=TextureWrapMode.Repeat;t.maxTextureSize=assetPath.Contains("Marble")?4096:1024;
            t.textureCompression=TextureImporterCompression.CompressedHQ;
            if(assetPath.Contains("nor_gl")){t.textureType=TextureImporterType.NormalMap;t.sRGBTexture=false;}
            else t.sRGBTexture=!assetPath.Contains("rough");
        }
    }
}
