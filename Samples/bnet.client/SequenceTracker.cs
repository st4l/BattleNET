using System;

namespace BNet.Client
{
    /// <summary>
    ///     Uses a byte buffer to store the last n sequence numbers
    ///     passed to it with Push, and provides a facility to check
    ///     whether a sequenceNumber was recorded.
    /// </summary>
    internal class SequenceTracker
    {
        private readonly byte[] buffer;
        private readonly int capacity;
        private int current = -1;
        private int max = -1;

        public SequenceTracker()
        {
            this.capacity = 100;
            this.buffer = new byte[100];
        }

        public SequenceTracker(int capacity)
        {
            this.capacity = capacity;
            this.buffer = new byte[capacity];
        }


        /// <summary>
        ///     Stores a sequence number for tracking. Discards old
        ///     sequence numbers that no longer fit in the buffer.
        /// </summary>
        /// <param name="sequenceNumber">The sequence number to track.</param>
        public void Push(byte sequenceNumber)
        {
            lock (this)
            {
                this.current++;
                if (this.current == this.capacity)
                {
                    this.current = 0;
                }

                // keep track of the last index used, will only grow until the 
                // first time we hit the buffer capacity
                if (this.current > this.max)
                {
                    this.max = this.current;
                }

                Buffer.SetByte(this.buffer, this.current, sequenceNumber);
            }
        }


        /// <summary>
        ///     Checks whether the specified sequence number has been
        ///     stored in this <see cref="SequenceTracker"/>.
        /// </summary>
        /// <param name="sequenceNumber">The sequence number to search for.</param>
        /// <returns>True if the sequence number was found; false otherwise.</returns>
        public bool Contains(byte sequenceNumber)
        {
            // search from current index to 0
            for (int i = this.current; i >= 0; i--)
            {
                if (Buffer.GetByte(this.buffer, i) == sequenceNumber)
                {
                    return true;
                }
            }

            // search from the maximum index used to the current index, exclusive
            for (int i = this.max; i > current; i--)
            {
                if (Buffer.GetByte(this.buffer, i) == sequenceNumber)
                {
                    return true;
                }
            }

            return false;
        }
    }
}