using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// A worker thread that works the world out ahead of you, so the game doesn't stutter: the chunks about to
    /// load (their tiles, heights, ground and map colours, all kept in the overworld's shared caches, so building
    /// them is quick when their turn comes), and the world map's pictures. Only the world's generator runs here
    /// (Core: Overworld, which is safe to share); everything Unity's (meshes, textures) stays on the main thread.
    public sealed class WorldWorker
    {
        readonly Overworld ow;
        readonly Thread thread;
        readonly object gate = new object();
        readonly Queue<Action> jobs = new Queue<Action>();            // asked for (the map's pictures): first
        readonly List<long> wanted = new List<long>();               // chunks to work out ahead, nearest first
        readonly ConcurrentDictionary<long, Color32[]> ready = new ConcurrentDictionary<long, Color32[]>();  // done: their map colours
        volatile bool stop;

        public WorldWorker(Overworld ow)
        {
            this.ow = ow;
            thread = new Thread(Loop) { IsBackground = true, Name = "World worker", Priority = System.Threading.ThreadPriority.BelowNormal };
            thread.Start();
        }

        /// The chunks to work out ahead (nearest first), in place of the last lot asked for.
        public void Want(List<long> chunks)
        {
            lock (gate)
            {
                wanted.Clear();
                foreach (var c in chunks)
                    if (!ready.ContainsKey(c))
                        wanted.Add(c);
                Monitor.Pulse(gate);
            }
        }

        /// Runs a job on the worker (before any chunks still waiting).
        public void Run(Action job)
        {
            lock (gate)
            {
                jobs.Enqueue(job);
                Monitor.Pulse(gate);
            }
        }

        /// Whether a chunk's been worked out (it'll build quickly).
        public bool IsReady(long chunk) => ready.ContainsKey(chunk);

        /// A worked-out chunk's map colours (and forgets them), or null if it hasn't been.
        public Color32[] TakeColours(long chunk) => ready.TryRemove(chunk, out var c) ? c : null;

        public void Stop()
        {
            stop = true;
            lock (gate)
                Monitor.Pulse(gate);
        }

        void Loop()
        {
            while (!stop)
            {
                Action job = null;
                long chunk = 0;
                bool haveChunk = false;
                lock (gate)
                {
                    while (!stop && jobs.Count == 0 && wanted.Count == 0)
                        Monitor.Wait(gate);
                    if (stop)
                        return;
                    if (jobs.Count > 0)
                        job = jobs.Dequeue();
                    else
                    {
                        chunk = wanted[0];
                        wanted.RemoveAt(0);
                        haveChunk = true;
                    }
                }
                try
                {
                    if (job != null)
                        job();
                    else if (haveChunk && !ready.ContainsKey(chunk))
                        WorkOut(chunk);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("World worker: " + e);
                }
            }
        }

        /// Works out what building a chunk will ask of the generator: its tiles and a few round it (roads' dirt,
        /// shores and trees look at their neighbours), its corners' heights and ground, and its map colours.
        void WorkOut(long key)
        {
            int cx = (int)(key >> 32), cz = (int)(uint)key, size = Overworld.ChunkSize;
            int x0 = cx * size, z0 = cz * size;
            for (int x = -3; x < size + 3; x++)
                for (int z = -3; z < size + 3; z++)
                    ow.At(new Tile(x0 + x, z0 + z));
            for (int i = 0; i <= size; i++)
                for (int j = 0; j <= size; j++)
                    ow.GroundWeights(x0 - 0.5 + i, z0 - 0.5 + j, out _, out _, out _);
            for (int x = -1; x <= size; x++)
                for (int z = -1; z <= size; z++)
                {
                    var t = new Tile(x0 + x, z0 + z);
                    ow.BiomeAt(t);
                    ow.Danger(t);
                }
            if (ready.Count > 4096)
                ready.Clear();
            ready[key] = World.ChunkColoursFor(ow, cx, cz);
        }
    }
}
