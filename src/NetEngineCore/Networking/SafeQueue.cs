// Net 4.X has ConcurrentQueue, but ConcurrentQueue has no TryDequeueAll method,
// which makes SafeQueue twice as fast for the send thread.
//
// uMMORPG 450 CCU
//   SafeQueue:       900-1440ms latency
//   ConcurrentQueue:     2000ms latency
//
// It's also noticeable in the LoadTest project, which hardly handles 300 CCU
// with ConcurrentQueue!

using System.Collections.Generic;

namespace NetEngineCore.Networking {
    public class SafeQueue<T> {
        private readonly Queue<T> _queue = new Queue<T>();

        // for statistics. don't call Count and assume that it's the same after the
        // call.
        public int Count {
            get {
                lock (_queue) {
                    return _queue.Count;
                }
            }
        }

        /// <summary>
        /// Enqueue.
        /// </summary>
        /// <param name="item"></param>
        public void Enqueue(T item) {
            lock (_queue) {
                _queue.Enqueue(item);
            }
        }

        // can't check .Count before doing Dequeue because it might change inbetween,
        // so we need a TryDequeue
        public bool TryDequeue(out T result) {
            lock (_queue) {
                result = default(T);
                if (_queue.Count > 0) {
                    result = _queue.Dequeue();
                    return true;
                }

                return false;
            }
        }

        // for when we want to dequeue and remove all of them at once without
        // locking every single TryDequeue.
        public bool TryDequeueAll(out T[] result) {
            lock (_queue) {
                result = _queue.ToArray();
                _queue.Clear();
                return result.Length > 0;
            }
        }

        /// <summary>
        /// Clear the queue.
        /// </summary>
        public void Clear() {
            lock (_queue) {
                _queue.Clear();
            }
        }
    }
}