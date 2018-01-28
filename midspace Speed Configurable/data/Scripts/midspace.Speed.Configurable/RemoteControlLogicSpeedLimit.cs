namespace midspace.Speed.ConfigurableScript
{
    using Sandbox.Common.ObjectBuilders;
    using Sandbox.ModAPI;
    using Sandbox.ModAPI.Interfaces.Terminal;
    using System.Collections.Generic;
    using System.Linq;
    using VRage.Game.Components;
    using VRage.ModAPI;
    using VRage.ObjectBuilders;

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_RemoteControl), false)]
    public class RemoteControlLogicSpeedLimit : MyGameLogicComponent
    {
        #region fields

        private MyObjectBuilder_EntityBase _objectBuilder;
        private bool _isInitilized;

        // this is to hold the initial value on game load, and prevent the value from changing if it's configured mid-game and saved without restarting yet.
        private static bool _staticIsInitialized;
        private static float _initialRemoteControlMaxSpeed;

        private IMyRemoteControl _remoteControlEntity;

        #endregion

        // This code will run on all clients and the server, so we need to isolate it to the server only.
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            _objectBuilder = objectBuilder;
            this.NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;

            if (!_staticIsInitialized)
            {
                _staticIsInitialized = true;
                _initialRemoteControlMaxSpeed = (float)ConfigurableSpeedComponentLogic.Instance.EnvironmentComponent.RemoteControlMaxSpeed;
            }

            if (!_isInitilized)
            {
                // Use this space to initialize and hook up events. NOT TO PROCESS ANYTHING.
                _isInitilized = true;

                if (_initialRemoteControlMaxSpeed > 0)
                {
                    _remoteControlEntity = (IMyRemoteControl)Entity;

                    List<IMyTerminalControl> controls;
                    MyAPIGateway.TerminalControls.GetControls<IMyRemoteControl>(out controls);

                    //VRage.Utils.MyLog.Default.WriteLine($"#### SpeedLimit {_remoteControlEntity.SpeedLimit} {_initialRemoteControlMaxSpeed}");

                    IMyTerminalControl control = controls.FirstOrDefault(c => c.Id == "SpeedLimit");
                    IMyTerminalControlSlider sliderControl = control as IMyTerminalControlSlider;
                    // control limits are set universally and cannot be applied individually.
                    sliderControl?.SetLimits(0, _initialRemoteControlMaxSpeed);
                }
            }
        }

        public override void UpdateOnceBeforeFrame()
        {
            if (_staticIsInitialized && _remoteControlEntity != null)
            {
                // reset this cube's existing SpeedLimit to the max if it is set too high.
                if (_remoteControlEntity.SpeedLimit > _initialRemoteControlMaxSpeed)
                    _remoteControlEntity.SpeedLimit = _initialRemoteControlMaxSpeed;
            }
        }

        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return _objectBuilder;
        }
    }
}