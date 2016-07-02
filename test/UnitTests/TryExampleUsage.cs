using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nito.AsyncEx;

namespace UnitTests
{
    public class TryExampleUsage
    {
        public void AMRE()
        {
            var amre = new AsyncManualResetEvent();
            if (amre.TryWait()) { }
            if (amre.IsSet) { } // Same semantics
        }

        public void AARE()
        {
            var aare = new AsyncAutoResetEvent();
            if (aare.TryWait()) { }
            if (aare.IsSet) { } // NOT the same semantics
        }

        public void AS()
        {
            var sem = new AsyncSemaphore(2);
            if (sem.TryWait()) { }
            if (sem.CurrentCount > 0) { } // NOT the same semantics
        }

        public void ACE()
        {
            var ace = new AsyncCountdownEvent(0);
            if (ace.TryWait()) { }
        }

        public void AL()
        {
            var al = new AsyncLock();
            using (var key = al.TryLock())
            {
                if (key) { }
            }

            // Potential misuse
            using (al.TryLock())
            {
            }
        }

        // AsyncMonitor and AsyncReaderWriterLock have same usage and potential misuse as AsyncLock.

        public void APCQ_Enqueue()
        {
            var apcq = new AsyncProducerConsumerQueue<int>();

            if (apcq.TryEnqueue(13)) { } // Throws if queue has completed.

            var result = apcq.TryAttemptEnqueue(13);
            if (result) // False if queue is full and not completed yet.
            {
                if (result.Result) { } // False if queue has completed.
            }

            var result2 = apcq.TryDequeue(); // Throws if queue has completed.
            if (result2)
            {
                int value = result2.Result;
            }

            var result3 = apcq.TryAttemptDequeue();
            if (result3) // False if queue is empty and not completed yet.
            {
                if (result3.Result) { } // False if queue has completed.
            }
        }
    }
}
