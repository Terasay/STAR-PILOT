using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace StarPilot.Editor
{
    public static class StarPilotTools
    {
        public const string ScenePath="Assets/StarPilot/Scenes/StarPilot_Demo.unity";
        const string XrFolder="Assets/StarPilot/XR";

        [InitializeOnLoadMethod]
        static void PrepareNewProject()
        {
            if(Application.isBatchMode)return;
            EditorApplication.delayCall+=()=>
            {
                if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)return;
                EnsureFlightGrade();
                EnsureArtImports();
                if(EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey,out XRGeneralSettingsPerBuildTarget existing) &&
                    existing.SettingsForBuildTarget(BuildTargetGroup.Standalone)?.Manager!=null)return;
                try { ConfigureXr(); }
                catch(Exception ex) { Debug.LogWarning("[STAR PILOT] Automatic XR setup could not finish. Use STAR PILOT > Configure PC OpenXR. "+ex.Message); }
            };
        }

        static void EnsureFlightGrade()
        {
            const string path="Assets/StarPilot/Resources/StarPilot/FlightGrade.asset";
            if(AssetDatabase.LoadAssetAtPath<VolumeProfile>(path))return;
            var profile=ScriptableObject.CreateInstance<VolumeProfile>();profile.name="FlightGrade";
            AssetDatabase.CreateAsset(profile,path);
            var tone=profile.Add<Tonemapping>(true);tone.mode.Override(TonemappingMode.ACES);
            var bloom=profile.Add<Bloom>(true);bloom.threshold.Override(1.15f);bloom.intensity.Override(.28f);bloom.scatter.Override(.65f);
            var vignette=profile.Add<Vignette>(true);vignette.intensity.Override(.18f);vignette.smoothness.Override(.5f);
            foreach(var component in profile.components)AssetDatabase.AddObjectToAsset(component,profile);
            EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();
        }

        static void EnsureArtImports()
        {
            const string folder="Assets/StarPilot/Resources/StarPilot/Textures";
            foreach(string path in Directory.GetFiles(folder,"*.jpg"))
            {
                string assetPath=path.Replace('\\','/');
                var importer=AssetImporter.GetAtPath(assetPath) as TextureImporter;if(importer==null)continue;
                bool normal=assetPath.Contains("nor_gl"),srgb=!normal && !assetPath.Contains("rough");
                int size=assetPath.Contains("Marble")?4096:assetPath.Contains("Rock_")?1024:2048;
                var type=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
                if(importer.textureType==type && importer.sRGBTexture==srgb && importer.maxTextureSize==size && importer.anisoLevel==8)continue;
                importer.textureType=type;importer.sRGBTexture=srgb;importer.maxTextureSize=size;importer.anisoLevel=8;
                importer.mipmapEnabled=true;importer.filterMode=FilterMode.Trilinear;importer.textureCompression=TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
        }

        [MenuItem("STAR PILOT/Open Flight Demo",priority=0)]
        public static void OpenDemo()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("STAR PILOT/Configure PC OpenXR",priority=10)]
        public static void ConfigureXr()
        {
            Directory.CreateDirectory(XrFolder);AssetDatabase.Refresh();
            if(!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey,out XRGeneralSettingsPerBuildTarget perTarget))
            {
                perTarget=ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perTarget,XrFolder+"/XRGeneralSettings.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey,perTarget,true);
            }
            var settings=perTarget.SettingsForBuildTarget(BuildTargetGroup.Standalone);
            if(settings==null)
            {
                settings=ScriptableObject.CreateInstance<XRGeneralSettings>();settings.name="PC XR General Settings";
                AssetDatabase.AddObjectToAsset(settings,perTarget);
                perTarget.SetSettingsForBuildTarget(BuildTargetGroup.Standalone,settings);
            }
            if(settings.Manager==null)
            {
                var manager=ScriptableObject.CreateInstance<XRManagerSettings>();manager.name="PC XR Manager";
                AssetDatabase.AddObjectToAsset(manager,perTarget);settings.Manager=manager;
            }
            // Desktop is the default. The VR menu button owns the loader lifecycle.
            settings.InitManagerOnStart=false;settings.Manager.automaticLoading=false;settings.Manager.automaticRunning=false;
            if(!XRPackageMetadataStore.AssignLoader(settings.Manager,"UnityEngine.XR.OpenXR.OpenXRLoader",BuildTargetGroup.Standalone))
                throw new InvalidOperationException("Could not assign PC OpenXR loader.");
            var openxr=OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
            if(openxr==null)throw new InvalidOperationException("OpenXR settings were not created.");
            foreach(var feature in openxr.GetFeatures<UnityEngine.XR.OpenXR.Features.OpenXRFeature>())
            {
                if(feature is OculusTouchControllerProfile || feature is ValveIndexControllerProfile ||
                    feature is MicrosoftMotionControllerProfile || feature is KHRSimpleControllerProfile ||
                    feature is HTCViveControllerProfile)feature.enabled=true;
            }
            EditorUtility.SetDirty(settings);EditorUtility.SetDirty(settings.Manager);EditorUtility.SetDirty(perTarget);EditorUtility.SetDirty(openxr);
            AssetDatabase.SaveAssets();
            Debug.Log("[STAR PILOT] PC OpenXR configured. Desktop starts without a headset; VR starts on request.");
        }

        [MenuItem("STAR PILOT/Build Windows Demo",priority=20)]
        public static void BuildWindows()
        {
            EnsureFlightGrade();
            EnsureArtImports();
            ConfigureXr();
            PlayerSettings.productName="STAR PILOT";
            PlayerSettings.bundleVersion="0.3.0";
            PlayerSettings.companyName="Star Pilot Team";
            PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;
            PlayerSettings.fullScreenMode=FullScreenMode.FullScreenWindow;
            PlayerSettings.runInBackground=true;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{GraphicsDeviceType.Direct3D11});
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            Directory.CreateDirectory("Builds/Windows");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes=new[]{ScenePath},locationPathName="Builds/Windows/STAR PILOT.exe",
                target=BuildTarget.StandaloneWindows64,options=BuildOptions.None
            });
            File.WriteAllText("Builds/build-result.txt",$"{report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nSize: {report.summary.totalSize}");
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("STAR PILOT Windows build failed.");
        }

        public static void BatchBuild()
        {
            try { BuildWindows();EditorApplication.Exit(0); }
            catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}
        }
    }
}
