namespace midspace.Speed.ConfigurableScript
{
    using Sandbox.Common.ObjectBuilders;
    using Sandbox.ModAPI;
    using Sandbox.ModAPI.Interfaces.Terminal;
    using System.Collections.Generic;
    using System.Linq;
    using VRage.Game.Components;
    using VRage.ObjectBuilders;

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_RemoteControl), false)]
    public class RemoteControlLogicSpeedLimit : MyGameLogicComponent
    {
        #region fields

        private MyObjectBuilder_EntityBase _objectBuilder;
        private bool _isInitilized;

        // this is to hold the initial value on game load, and prevent the value from changing if it's configured mid-game and saved without restarting yet.
        private static bool _staticIsInitialized;
        private static decimal _initialRemoteControlMaxSpeed;

        //private IMyRemoteControl _remoteControlEntity;

        #endregion

        // This code will run on all clients and the server, so we need to isolate it to the server only.
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            _objectBuilder = objectBuilder;

            if (!_staticIsInitialized)
            {
                _staticIsInitialized = true;
                _initialRemoteControlMaxSpeed = ConfigurableSpeedComponentLogic.Instance.EnvironmentComponent.RemoteControlMaxSpeed;
            }

            if (!_isInitilized)
            {
                // Use this space to initialize and hook up events. NOT TO PROCESS ANYTHING.
                _isInitilized = true;

                //_remoteControlEntity = (IMyRemoteControl)Entity;

                List<IMyTerminalControl> controls;
                MyAPIGateway.TerminalControls.GetControls<IMyRemoteControl>(out controls);

                IMyTerminalControl control = controls.FirstOrDefault(c => c.Id == "SpeedLimit");
                IMyTerminalControlSlider sliderControl = control as IMyTerminalControlSlider;
                if (sliderControl != null)
                {
                    // control limits are set universally and cannot be applied individually.
                    if (ConfigurableSpeedComponentLogic.Instance.EnvironmentComponent.RemoteControlMaxSpeed > 0)
                    {
                        sliderControl.SetLimits(0, (float)_initialRemoteControlMaxSpeed);
                    }
                }
            }
        }

        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return _objectBuilder;
        }
    }
}