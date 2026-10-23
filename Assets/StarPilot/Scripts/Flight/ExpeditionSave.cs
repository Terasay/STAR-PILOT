using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace StarPilot
{
    [Serializable]
    public sealed class ExpeditionSave
    {
        public int Version=2,GeneratorVersion=SectorLayout.Version,Seed=1729;
        public int Credits,Delivered,Data,Cargo,CargoValue;
        public ResearchMission Mission;
        public List<string> Scanned=new List<string>(),Collected=new List<string>();

        public static string DefaultPath()
        {
            string[] args=Environment.GetCommandLineArgs();int output=Array.IndexOf(args,"-testOutput");
            if(Array.IndexOf(args,"-starPilotSmokeTest")>=0 && output>=0 && output+1<args.Length)
                return Path.Combine(args[output+1],"expedition-test-save.json");
            return Path.Combine(Application.persistentDataPath,"expedition-v1.json");
        }
        public static ExpeditionSave Load(string path)
        {
            if(!File.Exists(path))return new ExpeditionSave();
            var save=JsonUtility.FromJson<ExpeditionSave>(File.ReadAllText(path));
            if(save==null || (save.Version!=1 && save.Version!=2) || save.GeneratorVersion!=SectorLayout.Version || save.Scanned==null || save.Collected==null ||
                save.Credits<0 || save.Cargo<0 || save.Cargo>10 || save.CargoValue<0 || save.Data<0 || save.Delivered<0)
                throw new InvalidDataException("Unsupported or damaged expedition save; original file preserved.");
            if(save.Version==1){save.Version=2;save.Mission=null;}
            if(save.Mission!=null && (!save.Mission.IsValid() || save.Cargo+(save.Mission.Carrying?ResearchMission.CargoUnits:0)>10))
                throw new InvalidDataException("Damaged research mission; original file preserved.");
            return save;
        }
        public void Write(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp=path+".tmp";File.WriteAllText(temp,JsonUtility.ToJson(this,true));
            if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);
        }
    }
}
