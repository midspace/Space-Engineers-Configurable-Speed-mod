namespace midspace.Speed.ConfigurableScript.Messages
{
    using System;
    using System.Xml.Serialization;
    using ProtoBuf;

    // ALL CLASSES DERIVED FROM MessageBase MUST BE ADDED HERE
    [XmlInclude(typeof(MessageClientDialogMessage))]
    [XmlInclude(typeof(MessageClientTextMessage))]
    [XmlInclude(typeof(MessageConfig))]
    [ProtoContract]
    [ProtoInclude(101, typeof(MessageClientDialogMessage))]
    [ProtoInclude(102, typeof(MessageClientTextMessage))]
    [ProtoInclude(103, typeof(MessageConfig))]
    public abstract class MessageBase
    {
        #region properties

        /// <summary>
        /// The SteamId of the message's sender. Note that this will be set when the message is sent, so there is no need for setting it otherwise.
        /// </summary>
        [ProtoMember(1)]
        public ulong SenderSteamId;

        /// <summary>
        /// The display name of the message sender.
        /// </summary>
        [ProtoMember(2)]
        public string SenderDisplayName;

        /// <summary>
        /// The current display language of the sender.
        /// </summary>
        [ProtoMember(3)]
        public int SenderLanguage;

        /// <summary>
        /// Defines on which side the message should be processed. Note that this will be set when the message is sent, so there is no need for setting it otherwise.
        /// </summary>
        [ProtoMember(4)]
        public MessageSide Side;

        #endregion

        ///// <summary>
        ///// This will allow the serializer to automatically execute the Action in the same step as Deserialization,
        ///// and reduce the message handling in the ConnectionHelper.
        ///// Exception handling ConnectionHelper would have to be moved here too.
        ///// </summary>
        //[ProtoAfterDeserialization] // not yet whitelisted.
        //void AfterDeserialization() // is not invoked after deserialization from xml
        //{
        //    InvokeProcessing();
        //}

        public void InvokeProcessing()
        {
            switch (Side)
            {
                case MessageSide.ClientSide:
                    InvokeClientProcessing();
                    break;
                case MessageSide.ServerSide:
                    InvokeServerProcessing();
                    break;
            }
        }

        private void InvokeClientProcessing()
        {
            ConfigurableSpeedComponentLogic.Instance.ClientLogger.WriteVerbose("Received - {0}", this.GetType().Name);
            try
            {
                ProcessClient();
            }
            catch (Exception ex)
            {
                // TODO: send error to server and notify admins
                ConfigurableSpeedComponentLogic.Instance.ClientLogger.WriteException(ex, "Could not process message on Client.");
            }
        }

        private void InvokeServerProcessing()
        {
            ConfigurableSpeedComponentLogic.Instance.ServerLogger.WriteVerbose("Received - {0}", this.GetType().Name);
            try
            {
                ProcessServer();
            }
            catch (Exception ex)
            {
                ConfigurableSpeedComponentLogic.Instance.ServerLogger.WriteException(ex, "Could not process message on Server.");
            }
        }

        public abstract void ProcessClient();
        public abstract void ProcessServer();
    }
}
