﻿using System;
using System.Collections.Generic;
using System.Threading;

namespace LiteDB.Studio
{
    public class TaskData
    {
        public const int RESULT_LIMIT = 1000;

        public string Title => Id > 0 ? Id.ToString() : "+";
        public int Id { get; set; }
        public bool Executing { get; set; } = false;

        public string EditorContent { get; set; } = "";
        public string SelectedTab { get; set; } = "";
        public (int, int) Position { get; set; }

        public string Sql { get; set; } = "";
        public string Collection { get; set; } = "";
        public List<BsonValue> Result { get; set; } = null;
        public BsonDocument Parameters { get; set; } = new BsonDocument();

        public bool LimitExceeded { get; set; }
        public Exception Exception { get; set; } = null;
        public TimeSpan Elapsed { get; set; } = TimeSpan.Zero;

        public bool IsGridLoaded = false;
        public bool IsTextLoaded = false;
        public bool IsParametersLoaded = false;

        public Thread Thread { get; set; }
        public bool ThreadRunning { get; set; } = true;
        public ManualResetEventSlim WaitHandle = new ManualResetEventSlim(false);

        public void ReadResult(IBsonDataReader reader)
        {
            this.Result = new List<BsonValue>();
            this.LimitExceeded = false;
            this.Collection = reader.Collection;

            var index = 0;

            while (reader.Read())
            {
                if (index++ >= RESULT_LIMIT)
                {
                    this.LimitExceeded = true;
                    break;
                }

                this.Result.Add(reader.Current);
            }
        }
    }
}
