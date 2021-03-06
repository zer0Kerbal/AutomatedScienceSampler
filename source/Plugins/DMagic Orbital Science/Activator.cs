﻿using DMagic;
using DMagic.Part_Modules;
using System;
using System.Collections.Generic;
using System.Text;

namespace KerboKatz.ASS.DMOS
{
    public class Activator : IScienceActivator
    {
        private AutomatedScienceSampler _AutomatedScienceSamplerInstance;

        AutomatedScienceSampler IScienceActivator.AutomatedScienceSampler
        {
            get { return _AutomatedScienceSamplerInstance; }
            set { _AutomatedScienceSamplerInstance = value; }
        }

        public bool CanRunExperiment(ModuleScienceExperiment baseExperiment, float currentScienceValue)
        {
            var currentExperiment = baseExperiment as DMModuleScienceAnimate;
            if (currentScienceValue < _AutomatedScienceSamplerInstance.craftSettings.threshold)
            {
                Log(currentExperiment.experimentID, ": Science value is less than cutoff threshold: ", currentScienceValue, "<", _AutomatedScienceSamplerInstance.craftSettings.threshold);
                return false;
            }
            if (!currentExperiment.rerunnable && !_AutomatedScienceSamplerInstance.craftSettings.oneTimeOnly)
            {
                Log(currentExperiment.experimentID, ": Runing rerunable experiments is disabled");
                return false;
            }
            if (currentExperiment.Inoperable)
            {
                Log(currentExperiment.experimentID, ": Experiment is inoperable");
                return false;
            }
            if (currentExperiment.Deployed)
            {
                Log(currentExperiment.experimentID, ": Experiment is deployed");
                return false;
            }
            if (!currentExperiment.experiment.IsUnlocked())
            {
                Log(currentExperiment.experimentID, ": Experiment is locked");
                return false;
            }
            if (!string.IsNullOrEmpty(currentExperiment.animationName))
            {
                var anim = currentExperiment.part.FindModelAnimators(currentExperiment.animationName)[0];
                if (anim.isPlaying)
                {
                    Log(currentExperiment.experimentID, ": Animation is playing");
                    return false;
                }
            }
            return DMAPI.experimentCanConduct(currentExperiment);
        }

        public void DeployExperiment(ModuleScienceExperiment baseExperiment)
        {
            var currentExperiment = baseExperiment as DMModuleScienceAnimate;
            DMAPI.deployDMExperiment(currentExperiment, _AutomatedScienceSamplerInstance.craftSettings.hideScienceDialog);
        }

        public ScienceSubject GetScienceSubject(ModuleScienceExperiment baseExperiment)
        {
            var currentExperiment = baseExperiment as DMModuleScienceAnimate;
            if (DMAPI.isAsteroidGrappled(baseExperiment))
            {
                return DMAPI.getAsteroidSubject(currentExperiment);
            }
            else
            {
                ExperimentSituations situation = ScienceUtil.GetExperimentSituation(FlightGlobals.ActiveVessel);
                var biome = DMAPI.getBiome(baseExperiment, situation);
                if (biome == null)
                {
                    Log("Biome is null.");
                    return null;
                }
                var scienceSubject = ResearchAndDevelopment.GetExperimentSubject(ResearchAndDevelopment.GetExperiment(currentExperiment.experimentID), situation, FlightGlobals.currentMainBody, biome, ScienceUtil.GetBiomedisplayName(FlightGlobals.currentMainBody, biome));
                Log(biome, "_", situation, "_", scienceSubject == null);
                return scienceSubject;
            }
        }

        public float GetScienceValue(ModuleScienceExperiment baseExperiment, Dictionary<string, int> shipCotainsExperiments, ScienceSubject currentScienceSubject)
        {
            var currentExperiment = baseExperiment as DMModuleScienceAnimate;
            var scienceExperiment = ResearchAndDevelopment.GetExperiment(baseExperiment.experimentID);
            return Utilities.Science.GetScienceValue(shipCotainsExperiments, scienceExperiment, currentScienceSubject) * currentExperiment.totalScienceLevel;
            /*if (DMAPI.isAsteroidGrappled(currentExperiment))
            {
              return Utilities.Science.GetScienceValue(shipCotainsExperiments, scienceExperiment, currentScienceSubject, null, GetNextScienceValue);
            }
            else
            {
              return Utilities.Science.GetScienceValue(shipCotainsExperiments, scienceExperiment, currentScienceSubject);
            }*/
        }

        public bool CanReset(ModuleScienceExperiment baseExperiment)
        {
            var currentExperiment = baseExperiment as DMModuleScienceAnimate;
            if (!currentExperiment.Inoperable)
            {
                Log(currentExperiment.experimentID, ": Experiment isn't inoperable");
                return false;
            }
            if (!currentExperiment.Deployed)
            {
                Log(currentExperiment.experimentID, ": Experiment isn't deployed!");
                return false;
            }
            if ((currentExperiment as IScienceDataContainer).GetScienceCount() > 0)
            {
                Log(currentExperiment.experimentID, ": Experiment has data!");
                return false;
            }
            if (!currentExperiment.resettable)
            {
                Log(currentExperiment.experimentID, ": Experiment isn't resetable");
                return false;
            }
            bool hasScientist = false;
            foreach (var crew in FlightGlobals.ActiveVessel.GetVesselCrew())
            {
                if (crew.trait == "Scientist")
                {
                    hasScientist = true;
                    break;
                }
            }
            if (!hasScientist)
            {
                Log(currentExperiment.experimentID, ": Vessel has no scientist");
                return false;
            }
            return true;
        }

        public void Reset(ModuleScienceExperiment baseExperiment)
        {
            var currentExperiment = baseExperiment as DMModuleScienceAnimate;
            Log(currentExperiment.experimentID, ": Reseting experiment");
            currentExperiment.ResetExperiment();
        }

        public bool CanTransfer(ModuleScienceExperiment baseExperiment, IScienceDataContainer moduleScienceContainer)
        {
            var currentExperiment = baseExperiment as DMModuleScienceAnimate;
            if ((currentExperiment as IScienceDataContainer).GetScienceCount() == 0)
            {
                Log(currentExperiment.experimentID, ": Experiment has no data skiping transfer. Data found: ", (currentExperiment as IScienceDataContainer).GetScienceCount(), "_", currentExperiment.experimentNumber);
                return false;
            }
            if (!currentExperiment.IsRerunnable())
            {
                if (!_AutomatedScienceSamplerInstance.craftSettings.transferAllData)
                {
                    Log(currentExperiment.experimentID, ": Experiment isn't rerunnable and transferAllData is turned off.");
                    return false;
                }
            }
            if (!_AutomatedScienceSamplerInstance.craftSettings.dumpDuplicates)
            {
                foreach (var data in (currentExperiment as IScienceDataContainer).GetData())
                {
                    if (moduleScienceContainer.HasData(data))
                    {
                        Log(currentExperiment.experimentID, ": Target already has experiment and dumping is disabled.");
                        return false;
                    }
                }
            }
            Log(currentExperiment.experimentID, ": We can transfer the science!");
            return true;
        }

        public void Transfer(ModuleScienceExperiment baseExperiment, IScienceDataContainer moduleScienceContainer)
        {
            var currentExperiment = baseExperiment as DMModuleScienceAnimate;
            Log(currentExperiment.experimentID, ": transfering");
            moduleScienceContainer.StoreData(currentExperiment, _AutomatedScienceSamplerInstance.craftSettings.dumpDuplicates);
        }

        public List<Type> GetValidTypes()
        {
            var types = new List<Type>();
            types.Add(typeof(DMModuleScienceAnimate));

            Utilities.LoopTroughAssemblies((type) =>
            {
                if (type.IsClass && !type.IsAbstract && type.IsSubclassOf(typeof(DMModuleScienceAnimate)))
                {
                    types.Add(type);
                }
            });
            return types;
        }

        private void Log(params object[] msg)
        {
            var debugStringBuilder = new StringBuilder();
            foreach (var debugString in msg)
            {
                debugStringBuilder.Append(debugString.ToString());
            }
            _AutomatedScienceSamplerInstance.Log("[DMagicOrbitalScience]", debugStringBuilder);
        }
    }
}