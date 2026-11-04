using UnityEditor;
using UnityEngine;

namespace Caribbean.EditorTools
{
    /// <summary>
    /// Настройки импорта FBX из Blender (Assets/Resources/Models):
    /// 1 юнит = 1 метр, меши читаемые (нужно для MeshCollider островов), без анимаций/камер/света.
    /// </summary>
    public class CaribbeanModelImport : AssetPostprocessor
    {
        public override uint GetVersion() => 2;

        void OnPreprocessModel()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/Models/")) return;
            var mi = assetImporter as ModelImporter;
            if (mi == null) return;
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.isReadable = true;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importAnimation = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.importBlendShapes = false;
            mi.importNormals = ModelImporterNormals.Import;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.addCollider = false;
        }
    }
}
