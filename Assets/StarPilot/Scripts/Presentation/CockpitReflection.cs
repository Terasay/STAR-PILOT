using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace StarPilot
{
    /// <summary>Capture practical lighting once; no six-camera reflection rendering during flight.</summary>
    public sealed class CockpitReflection : MonoBehaviour
    {
        IEnumerator Start()
        {
            yield return null;
            var probe=gameObject.AddComponent<ReflectionProbe>();
            probe.mode=ReflectionProbeMode.Realtime;probe.refreshMode=ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.resolution=128;probe.size=new Vector3(7,6,9);probe.center=Vector3.zero;
            probe.boxProjection=true;probe.hdr=true;probe.intensity=.7f;probe.nearClipPlane=.08f;probe.farClipPlane=18000;
            probe.clearFlags=ReflectionProbeClearFlags.Skybox;probe.RenderProbe();
        }
    }
}
