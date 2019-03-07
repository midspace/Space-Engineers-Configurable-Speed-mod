namespace midspace.Speed.ConfigurableScript
{
    using Messages;
    using Sandbox.Common.ObjectBuilders;
    using Sandbox.Definitions;
    using Sandbox.ModAPI;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using VRage.Collections;
    using VRage.Game;
    using VRage.Game.Components;

    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class ConfigurableSpeedComponentLogic : MySessionComponentBase
    {
        #region constants

        /// <summary>
        /// pattern defines speedconfig commands.
        /// </summary>
        private const string ConfigSpeedPattern = @"^(?<command>/configspeed)(?:\s+(?<config>((ResetAll)|(LargeShipMaxSpeed)|(LargeShipSpeed)|(LargeShip)|(Large)|(SmallShipMaxSpeed)|(SmallShipSpeed)|(SmallShip)|(Small)|(ThrustRatio)|(EnableThrustRatio)|(LockThrustRatio)|(MaxAllSpeed)|(MissileMinSpeed)|(MissileMin)|(MissileMaxSpeed)|(MissileMax)|(autopilotspeed)|(autopilotlimit)|(autopilot)|(remoteautopilotlimit)|(remoteautopilotspeed)|(remoteautopilot)|(remotecontrolmaxspeed)|(containerdropdeployheight)|(containerdeployheight)|(dropdeployheight)|(dropheight)|(respawnshipdeployheight)|(respawndeployheight)|(respawnheight)))(?:\s+(?<value>.+))?)?";

        private const string ShortSpeedPattern = @"^(?<command>(/maxspeed))(?:\s+(?<value>.+))";

        #endregion

        #region fields

        private bool _isInitialized;
        private bool _isClientRegistered;
        private bool _isServerRegistered;
        private readonly Action<byte[]> _messageHandler = new Action<byte[]>(HandleMessage);
        public static ConfigurableSpeedComponentLogic Instance;
        public TextLogger ServerLogger = new TextLogger(); // This is a dummy logger until Init() is called.
        public TextLogger ClientLogger = new TextLogger(); // This is a dummy logger until Init() is called.

        /// <summary>
        /// This will hold the EnvironmentDefinition at startup, so we have the default values.
        /// </summary>
        public MidspaceEnvironmentComponent DefaultDefinitionValues;

        /// <summary>
        /// The current values that are stored and read into the game.
        /// </summary>
        public MidspaceEnvironmentComponent EnvironmentComponent;

        /// <summary>
        /// The previous values before we start changing them.
        /// </summary>
        public MidspaceEnvironmentComponent OldEnvironmentComponent;

        /// <summary>
        /// Indicates the stage of the settings if we have changed any.
        /// </summary>
        public bool IsModified;

        #endregion

        #region attaching events and wiring up

        public override void UpdateBeforeSimulation()
        {
            try
            {
                //VRage.Utils.MyLog.Default.WriteLine("##Mod## ConfigurableSpeed UpdateBeforeSimulation");
                if (Instance == null)
                    Instance = this;

                // This needs to wait until the MyAPIGateway.Session.Player is created, as running on a Dedicated server can cause issues.
                // It would be nicer to just read a property that indicates this is a dedicated server, and simply return.
                if (!_isInitialized && MyAPIGateway.Session != null && MyAPIGateway.Session.Player != null)
                {
                    if (MyAPIGateway.Session.OnlineMode.Equals(MyOnlineModeEnum.OFFLINE)) // pretend single player instance is also server.
                        InitServer();
                    if (!MyAPIGateway.Session.OnlineMode.Equals(MyOnlineModeEnum.OFFLINE) && MyAPIGateway.Multiplayer.IsServer && !MyAPIGateway.Utilities.IsDedicated)
                        InitServer();
                    InitClient();
                }

                // Dedicated Server.
                if (!_isInitialized && MyAPIGateway.Utilities != null && MyAPIGateway.Multiplayer != null
                    && MyAPIGateway.Session != null && MyAPIGateway.Utilities.IsDedicated && MyAPIGateway.Multiplayer.IsServer)
                {
                    InitServer();
                    return;
                }

                base.UpdateBeforeSimulation();
            }
            catch (Exception ex)
            {
                ClientLogger.WriteException(ex);
                ServerLogger.WriteException(ex);
            }
        }

        private void InitClient()
        {
            _isInitialized = true; // Set this first to block any other calls from UpdateAfterSimulation().
            _isClientRegistered = true;
            ClientLogger.Init("ConfigurableSpeedClient.Log", false, 0); // comment this out if logging is not required for the Client.
            ClientLogger.WriteStart("ConfigurableSpeed Client Log Started");

            MyAPIGateway.Utilities.MessageEntered += GotMessage;

            if (MyAPIGateway.Multiplayer.MultiplayerActive && !_isServerRegistered) // if not the server, also need to register the messagehandler.
            {
                ClientLogger.WriteStart("RegisterMessageHandler");
                MyAPIGateway.Multiplayer.RegisterMessageHandler(SpeedConsts.ConnectionId, _messageHandler);
            }

            ClientLogger.Flush();
        }

        private void InitServer()
        {
            _isInitialized = true; // Set this first to block any other calls from UpdateAfterSimulation().
            _isServerRegistered = true;
            ServerLogger.Init("ConfigurableSpeedServer.Log", false, 0); // comment this out if logging is not required for the Server.
            ServerLogger.WriteStart("ConfigurableSpeed Server Log Started");
            ServerLogger.WriteInfo("ConfigurableSpeed Server Version {0}", SpeedConsts.ModCommunicationVersion);
            if (ServerLogger.IsActive)
                VRage.Utils.MyLog.Default.WriteLine(string.Format("##Mod## ConfigurableSpeed Server Logging File: {0}", ServerLogger.LogFile));

            ServerLogger.WriteStart("RegisterMessageHandler");
            MyAPIGateway.Multiplayer.RegisterMessageHandler(SpeedConsts.ConnectionId, _messageHandler);
            //MyAPIGateway.Entities.OnEntityAdd += Entities_OnEntityAdd;

            ServerLogger.Flush();
        }

        #endregion

        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            if (Instance == null)
                Instance = this;

            try
            {
                // This Variables are already loaded by this point, but unaccessible because we need Utilities.

                // Need to create the Utilities, as it isn't yet created by the game at this point.
                //MyModAPIHelper.OnSessionLoaded();

                if (MyAPIGateway.Utilities == null)
                    MyAPIGateway.Utilities = MyAPIUtilities.Static;
                //    MyAPIGateway.Utilities = new MyAPIUtilities();

                MyDefinitionId missileId = new MyDefinitionId(typeof(MyObjectBuilder_AmmoDefinition), "Missile");
                MyMissileAmmoDefinition ammoDefinition = MyDefinitionManager.Static.GetAmmoDefinition(missileId) as MyMissileAmmoDefinition;

                DefaultDefinitionValues = new MidspaceEnvironmentComponent
                {
                    LargeShipMaxSpeed = (decimal)MyDefinitionManager.Static.EnvironmentDefinition.LargeShipMaxSpeed,
                    SmallShipMaxSpeed = (decimal)MyDefinitionManager.Static.EnvironmentDefinition.SmallShipMaxSpeed,
                    MissileMinSpeed = (decimal)(ammoDefinition?.MissileInitialSpeed ?? 0),
                    MissileMaxSpeed = (decimal)(ammoDefinition?.DesiredSpeed ?? 0),
                    RemoteControlMaxSpeed = 100, // game hardcoded default in MyRemoteControl.CreateTerminalControls()
                    ContainerDropDeployHeight = MessageConfig.DefaultContainerDropDeployHeight,
                    RespawnShipDeployHeight = MessageConfig.DefaultRespawnShipDeployHeight
                };

                // Load the speed on both server and client.
                string xmlValue;
                if (MyAPIGateway.Utilities.GetVariable("MidspaceEnvironmentComponent", out xmlValue))
                {
                    EnvironmentComponent = MyAPIGateway.Utilities.SerializeFromXML<MidspaceEnvironmentComponent>(xmlValue);
                    if (EnvironmentComponent != null)
                    {
                        // Fix Defaults.
                        if (EnvironmentComponent.Version == 0)
                            EnvironmentComponent.Version = SpeedConsts.ModCommunicationVersion;
                        if (EnvironmentComponent.ThrustRatio <= 0)
                            EnvironmentComponent.ThrustRatio = 1;
                        if (EnvironmentComponent.GyroPowerMod <= 0)
                            EnvironmentComponent.GyroPowerMod = 1;
                        if (EnvironmentComponent.IonAirEfficient < 0 || EnvironmentComponent.IonAirEfficient > 1)
                            EnvironmentComponent.IonAirEfficient = 0;
                        if (EnvironmentComponent.AtmosphereSpaceEfficient < 0 || EnvironmentComponent.AtmosphereSpaceEfficient > 1)
                            EnvironmentComponent.AtmosphereSpaceEfficient = 0;
                        if (EnvironmentComponent.MissileMinSpeed == 0)
                            EnvironmentComponent.MissileMinSpeed = DefaultDefinitionValues.MissileMinSpeed;
                        if (EnvironmentComponent.MissileMaxSpeed == 0)
                            EnvironmentComponent.MissileMaxSpeed = DefaultDefinitionValues.MissileMaxSpeed;
                        if (EnvironmentComponent.RemoteControlMaxSpeed == 0)
                            EnvironmentComponent.RemoteControlMaxSpeed = DefaultDefinitionValues.RemoteControlMaxSpeed;
                        if (EnvironmentComponent.ContainerDropDeployHeight == 0)
                            EnvironmentComponent.ContainerDropDeployHeight = MessageConfig.DefaultContainerDropDeployHeight;
                        if (EnvironmentComponent.RespawnShipDeployHeight == 0)
                            EnvironmentComponent.RespawnShipDeployHeight = MessageConfig.DefaultRespawnShipDeployHeight;

                        // Apply settings.
                        if (EnvironmentComponent.LargeShipMaxSpeed > 0)
                            MyDefinitionManager.Static.EnvironmentDefinition.LargeShipMaxSpeed = (float)EnvironmentComponent.LargeShipMaxSpeed;
                        if (EnvironmentComponent.SmallShipMaxSpeed > 0)
                            MyDefinitionManager.Static.EnvironmentDefinition.SmallShipMaxSpeed = (float)EnvironmentComponent.SmallShipMaxSpeed;

                        if (EnvironmentComponent.EnableThrustRatio)
                        {
                            List<MyDefinitionBase> blocks = MyDefinitionManager.Static.GetAllDefinitions().Where(d => d is MyCubeBlockDefinition &&
                                                                (((MyCubeBlockDefinition) d).Id.TypeId == typeof (MyObjectBuilder_Thrust))).ToList();
                            foreach (var block in blocks)
                            {
                                MyThrustDefinition thrustBlock = (MyThrustDefinition)block;

                                /*
                                // thrustBlock.ThrusterType == // this only affects the sound type.

                                //thrustBlock.ResourceSinkGroup

                                //if (thrustBlock.NeedsAtmosphereForInfluence) // ??? might be indicative of atmosphic thruster.
                                if (thrustBlock.PropellerUse)
                                {
                                    // Is atmosphic thruster.

                                    // MinPlanetaryInfluence is the one to adjust.
                                    // the default of 0.3 takes it down into the gravity well, somewhere below where Low Oxygen starts.
                                    // at 0.0, it is at the cusp between Low Oxygen and No oxygen.
                                    // at -0.3, it allows the atmosphic thruster to work in Space.

                                    //thrustBlock.MinPlanetaryInfluence = -0.3f;       // 0.3  
                                    //thrustBlock.MaxPlanetaryInfluence = 1f;         // 1.0
                                    //thrustBlock.EffectivenessAtMinInfluence = 0f;   // 0.0
                                    //thrustBlock.EffectivenessAtMaxInfluence = 1f;   // 1.0
                                    //thrustBlock.NeedsAtmosphereForInfluence = true; // true
                                }
                                else
                                {
                                    // Is Ion or Hydrogen thruster.

                                    //thrustBlock.FuelConverter != null // ??? Hydrogen or other fuel propellant.

                                    //thrustBlock.MinPlanetaryInfluence = 0.0f;       // 0.0  
                                    //thrustBlock.MaxPlanetaryInfluence = 0.3f;         // 1.0
                                    //thrustBlock.EffectivenessAtMinInfluence = 1.0f;   // 1.0
                                    //thrustBlock.EffectivenessAtMaxInfluence = 0.0f;   // 0.3
                                }
                                */
                                thrustBlock.ForceMagnitude *= (float)EnvironmentComponent.ThrustRatio;
                            }
                        }

                        /*
                        // if enabled Gyro boost.
                        {
                            List<MyDefinitionBase> blocks = MyDefinitionManager.Static.GetAllDefinitions().Where(d => d is MyCubeBlockDefinition &&
                                    (((MyCubeBlockDefinition) d).Id.TypeId == typeof (MyObjectBuilder_Gyro))).ToList();
                            foreach (var block in blocks)
                            {
                                MyGyroDefinition gyroBlock = (MyGyroDefinition) block;
                                //gyroBlock.ForceMagnitude *= 100; // This works.
                            }
                        }
                        */

                        if (ammoDefinition != null && EnvironmentComponent.MissileMinSpeed > 0)
                            ammoDefinition.MissileInitialSpeed = (float)EnvironmentComponent.MissileMinSpeed;
                        if (ammoDefinition != null && EnvironmentComponent.MissileMaxSpeed > 0)
                            ammoDefinition.DesiredSpeed = (float)EnvironmentComponent.MissileMaxSpeed;

                        #region ContainerDropDeployHeight

                        // We're basically changing the ContainerDrop prefabs that are loaded in memory before any of them are spawned.
                        // This is not my preferred approach, as these could be altered (by other mods) or reset (reload from disc by the game or mods).
                        // The prefered approach is to modify the chute.DeployHeight after a container is spawned, but it is not whitelisted.
                        DictionaryReader<string, MyDropContainerDefinition> dropContainers = MyDefinitionManager.Static.GetDropContainerDefinitions();
                        foreach (var kvp in dropContainers)
                        {
                            foreach (MyObjectBuilder_CubeGrid grid in kvp.Value.Prefab.CubeGrids)
                            {
                                foreach (MyObjectBuilder_CubeBlock block in grid.CubeBlocks)
                                {
                                    MyObjectBuilder_Parachute chute = block as MyObjectBuilder_Parachute;
                                    if (chute != null)
                                    {
                                        if (chute.DeployHeight < (float)EnvironmentComponent.ContainerDropDeployHeight)
                                            chute.DeployHeight = (float)EnvironmentComponent.ContainerDropDeployHeight;
                                    }
                                }
                            }
                        }

                        #endregion

                        #region RespawnShipDeployHeight

                        DictionaryReader<string, MyRespawnShipDefinition> respawnShips = MyDefinitionManager.Static.GetRespawnShipDefinitions();

                        foreach (var kvp in respawnShips)
                        {
                            foreach (MyObjectBuilder_CubeGrid grid in kvp.Value.Prefab.CubeGrids)
                            {
                                foreach (MyObjectBuilder_CubeBlock block in grid.CubeBlocks)
                                {
                                    MyObjectBuilder_Parachute chute = block as MyObjectBuilder_Parachute;
                                    if (chute != null)
                                    {
                                        if (chute.DeployHeight < (float)EnvironmentComponent.RespawnShipDeployHeight)
                                            chute.DeployHeight = (float)EnvironmentComponent.RespawnShipDeployHeight;
                                    }
                                }
                            }
                        }

                        #endregion

                        OldEnvironmentComponent = EnvironmentComponent.Clone();
                        return;
                    }
                }

                // creates a new EnvironmentComponent if one was not found in the game Variables.
                EnvironmentComponent = new MidspaceEnvironmentComponent
                {
                    Version = SpeedConsts.ModCommunicationVersion,
                    LargeShipMaxSpeed = (decimal)MyDefinitionManager.Static.EnvironmentDefinition.LargeShipMaxSpeed,
                    SmallShipMaxSpeed = (decimal)MyDefinitionManager.Static.EnvironmentDefinition.SmallShipMaxSpeed,
                    EnableThrustRatio = false,
                    MissileMinSpeed = (decimal)(ammoDefinition?.MissileInitialSpeed ?? 0),
                    MissileMaxSpeed = (decimal)(ammoDefinition?.DesiredSpeed ?? 0),
                    ThrustRatio = 1,
                    RemoteControlMaxSpeed = 100,
                    ContainerDropDeployHeight = MessageConfig.DefaultContainerDropDeployHeight,
                    RespawnShipDeployHeight = MessageConfig.DefaultRespawnShipDeployHeight
                };
                OldEnvironmentComponent = EnvironmentComponent.Clone();
            }
            catch (Exception ex)
            {
                VRage.Utils.MyLog.Default.WriteLine("##Mod## ERROR " + ex.Message);

                // The Loggers doesn't actually exist yet, as Init is called before UpdateBeforeSimulation.
                // TODO: should rework the code to change this.
                //ClientLogger.WriteException(ex);
                //ServerLogger.WriteException(ex);
            }
        }

        #region detaching events

        protected override void UnloadData()
        {
            ClientLogger.WriteStop("Shutting down");
            ServerLogger.WriteStop("Shutting down");

            if (_isClientRegistered)
            {
                if (MyAPIGateway.Utilities != null)
                {
                    MyAPIGateway.Utilities.MessageEntered -= GotMessage;
                }

                if (!_isServerRegistered) // if not the server, also need to unregister the messagehandler.
                {
                    ClientLogger.WriteStop("UnregisterMessageHandler");
                    MyAPIGateway.Multiplayer.UnregisterMessageHandler(SpeedConsts.ConnectionId, _messageHandler);
                }

                ClientLogger.WriteStop("Log Closed");
                ClientLogger.Terminate();
            }

            if (_isServerRegistered)
            {
                ServerLogger.WriteStop("UnregisterMessageHandler");
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(SpeedConsts.ConnectionId, _messageHandler);
                //MyAPIGateway.Entities.OnEntityAdd -= Entities_OnEntityAdd;

                ServerLogger.WriteStop("Log Closed");
                ServerLogger.Terminate();
            }

            base.UnloadData();
        }

        public override void SaveData()
        {
            ClientLogger.WriteStop("SaveData");
            ServerLogger.WriteStop("SaveData");

            if (_isServerRegistered)
            {
                // Only save the speed back to the server duruing world save.
                var xmlValue = MyAPIGateway.Utilities.SerializeToXML(EnvironmentComponent);
                MyAPIGateway.Utilities.SetVariable("MidspaceEnvironmentComponent", xmlValue);
            }

            base.SaveData();
        }

        #endregion

        #region message handling

        private static void HandleMessage(byte[] message)
        {
            ConfigurableSpeedComponentLogic.Instance.ServerLogger.WriteVerbose("HandleMessage");
            ConfigurableSpeedComponentLogic.Instance.ClientLogger.WriteVerbose("HandleMessage");
            ConnectionHelper.ProcessData(message);
        }

        private void GotMessage(string messageText, ref bool sendToOthers)
        {
            try
            {
                // here is where we nail the echo back on commands "return" also exits us from processMessage
                if (ProcessMessage(messageText)) { sendToOthers = false; }
            }
            catch (Exception ex)
            {
                ClientLogger.WriteException(ex);
                MyAPIGateway.Utilities.ShowMessage("Error", "An exception has been logged in the file: {0}", ClientLogger.LogFileName);
            }
        }

        #endregion

        #region command list

        private bool ProcessMessage(string messageText)
        {
            #region configspeed

            if (MyAPIGateway.Session.Player.IsAdmin())
            {
                Match match = Regex.Match(messageText, ConfigSpeedPattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    MessageConfig.SendMessage(match.Groups["config"].Value, match.Groups["value"].Value);
                    return true;
                }

                match = Regex.Match(messageText, ShortSpeedPattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    MessageConfig.SendMessage("MaxAllSpeed", match.Groups["value"].Value);
                    return true;
                }
            }

            #endregion configspeed

            // it didnt start with help or anything else that matters so return false and get us out of here;
            return false;
        }

        #endregion command list

        //private void Entities_OnEntityAdd(IMyEntity obj)
        //{
        //    if (obj is IMyCubeGrid && obj.DisplayName.StartsWith("Container MK-"))
        //    {
        //        IMyCubeGrid cubeGrid = (IMyCubeGrid)obj;

        //        var blocks = new List<IMySlimBlock>();
        //        cubeGrid.GetBlocks(blocks, f => f.FatBlock is IMyParachute);

        //        foreach (var block in blocks)
        //        {
        //            IMyParachute chute = (IMyParachute)block.FatBlock;
        //            //if (chute.Atmosphere > 0)
        //            MyAPIGateway.Utilities.InvokeOnGameThread(() => { chute.OpenDoor(); });
        //            // MyParachute.DeployHeight is not whitelisted
        //        }
        //    }
        //}
    }
}