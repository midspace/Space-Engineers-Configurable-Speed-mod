namespace midspace.Speed.ConfigurableScript
{
    using System;
    using ProtoBuf;

    /// <summary>
    /// This is the class that is serialized and stored in the Sandbox.sbc file.
    /// </summary>
    [ProtoContract]
    [Serializable]
    public class MidspaceEnvironmentComponent
    {
        [ProtoMember(1)]
        public decimal LargeShipMaxSpeed { get; set; }

        [ProtoMember(2)]
        public decimal SmallShipMaxSpeed { get; set; }

        [ProtoMember(3)]
        public int Version { get; set; }

        [ProtoMember(4)]
        public bool EnableThrustRatio { get; set; }

        [ProtoMember(5)]
        public decimal ThrustRatio { get; set; }

        [ProtoMember(6)]
        public decimal GyroPowerMod { get; set; }

        [ProtoMember(7)]
        public decimal IonAirEfficient { get; set; }

        [ProtoMember(8)]
        public decimal AtmosphereSpaceEfficient { get; set; }

        // Unused as yet.
        //[ProtoMember(9)]
        //public decimal LargeThrusterOverride { get; set; }

        // Unused as yet.
        //[ProtoMember(10)]
        //public decimal SmallThrusterOverride { get; set; }

        // Unused as yet.
        //public bool Realism { get; set; }
    }
}
