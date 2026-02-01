using TableAutomation.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExcelToData
{
    internal enum DataExtractResultType
    {
        Success,
        Failed,
    }


    internal class DataExtractResult : Singleton<DataExtractResult>
    {
        private struct ResultInfo
        {
            public DataExtractResultType Result;
            public string TableName;
            public string Logs;
        }

        private Dictionary<DataExtractResultType, List<ResultInfo>> resultDict = new();

        public void AddResult(DataExtractResultType result, string tableName, string logs)
        {
            if (!resultDict.ContainsKey(result))
                resultDict[result] = new List<ResultInfo>();
        }

        public void ShowReulst()
        {
            foreach(var kvp in resultDict)
            {
                Logger.Instance.Log($"===== {kvp.Key} Results =====");
                foreach (var info in kvp.Value)
                {
                    Logger.Instance.Log($"Table: {info.TableName}");
                    Logger.Instance.Log(info.Logs);
                }
            }
        }

        public void Clear()
        {
            foreach(var key in resultDict.Keys.ToList())
            {
                resultDict[key].Clear();
            }
        }
    }
}
