using System;

 [Serializable]
    public struct InputCommand
    {
        public BufferedAction action;
        public float timestamp;

        public InputCommand(
            BufferedAction action,
            float timestamp)
        {
            this.action = action;
            this.timestamp = timestamp;
        }
    }