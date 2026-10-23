using UnityEngine;

namespace StarPilot
{
    public sealed class ExplorationSite : MonoBehaviour
    {
        public string Id;
        public SiteKind Kind;
        public Transform CargoVisual;
        public bool Scanned,Collected;
        public bool MissionObjective,IsRemote;
        public Vector3 DockPosition=>transform.TransformPoint(new Vector3(0,0,Id=="kepler"?-95:-58));
        public Vector3 Position=>Kind==SiteKind.Station?DockPosition:transform.position;
        public string Title=>Kind switch {SiteKind.Wreck=>"ОБЛОМКИ",SiteKind.Anomaly=>MissionObjective?"ЭХО СИГНАЛА":"АНОМАЛИЯ",SiteKind.Mineral=>"ОБРАЗЦЫ",SiteKind.Relay=>"АВАРИЙНЫЙ МАЯК",SiteKind.Archive=>"ЗОНД / АРХИВ",_=>"СТАНЦИЯ"};
        public bool Exhausted=>Kind==SiteKind.Anomaly?Scanned:Kind!=SiteKind.Station && Collected;
        public void RefreshVisual(){if(CargoVisual)CargoVisual.gameObject.SetActive(Scanned && !Collected);}
    }
}
