using System.Collections.Generic;
using System;
using System.Diagnostics;
using System.Text;

namespace AssetBundleFramework.Core
{
    public class Profiler
    {
        private static readonly Stopwatch MStopWatch = Stopwatch.StartNew();
        private static readonly StringBuilder MStringBuilder = new StringBuilder();
        private static readonly List<Profiler> MStack = new List<Profiler>();

        private List<Profiler> _mChildren;
        private readonly string _mName;
        private readonly int _mLevel;
        private long _mTimestamp;
        private long _mTime;
        private long _mCount;
        public Profiler(string name)
        {
            _mName = name;
            _mChildren = null;
            _mLevel = 0;
            _mTimestamp = -1;
            _mCount = 0;
            _mTime = 0;
        }

        private Profiler(string name, int level) : this(name)
        {
            _mLevel = level;
        }

        public Profiler CreateChild(string name)
        {
            _mChildren ??= new List<Profiler>();

            var profiler = new Profiler(name, _mLevel + 1);
            _mChildren.Add(profiler);
            return profiler;
        }

        public void Start()
        {
            if (_mTimestamp != -1)
            {
                throw new Exception($"{nameof(Profiler)} {nameof(Start)} error, Repeat Start, name: ${_mName}");
            }

            _mTimestamp = MStopWatch.ElapsedMilliseconds;
        }

        public void Stop() 
        {
            if (_mTimestamp == -1)
            {
                throw new Exception($"{nameof(Profiler)} {nameof(Start)} error, Repeat Stop, name: ${_mName}");
            }

            _mTime += MStopWatch.ElapsedMilliseconds - _mTimestamp;
            _mCount++;
            _mTimestamp = -1;
        }

        private void Format() 
        {   
            MStringBuilder.AppendLine();

            for(int i = 0; i < _mLevel; i++) 
            {
                MStringBuilder.Append(i < _mLevel - 1 ? "|  " : "|--");
            }

            MStringBuilder.Append($"{_mName} [Count: {_mCount}, Time: {(float)_mTime / TimeSpan.TicksPerMillisecond:F2}毫秒 {(float)_mTime / TimeSpan.TicksPerSecond:F2}秒 {(float)_mTime / TimeSpan.TicksPerMinute:F2}分]"); 
        }

        public override string ToString()
        {
            MStringBuilder.Clear();
            MStack.Clear();

            MStack.Add(this);
            while(MStack.Count > 0) 
            {
                int index = MStack.Count - 1;
                Profiler profiler = MStack[index];
                MStack.RemoveAt(index);

                profiler.Format();

                List<Profiler> children = profiler._mChildren;
                if(children != null)
                {
                    for(int i = children.Count - 1; i >= 0; i--)
                    {
                        MStack.Add(children[i]);
                    }
                }
            }

            return MStringBuilder.ToString();
        }
    }
}
