using System.Collections.Generic;
using UnityEngine;

namespace CoffeeBean.Purchase
{
    /// <summary>
    /// 已处理交易日志（去重用）：内存集合 + 可选 PlayerPrefs 持久化。
    /// 防止 ProcessPurchase/补发流程重入导致重复发货。
    /// </summary>
    public sealed class PurchaseJournal
    {
        private const string PrefsKey = "CoffeeBean.Purchase.Journal";
        private readonly HashSet<string> _ids = new HashSet<string>();

        /// <summary>是否持久化到 PlayerPrefs（崩溃后重启仍能去重）。默认开启。</summary>
        public bool UsePersistence = true;

        public PurchaseJournal()
        {
            if (UsePersistence) Load();
        }

        public bool Contains(string transactionId)
            => !string.IsNullOrEmpty(transactionId) && _ids.Contains(transactionId);

        public void Add(string transactionId)
        {
            if (string.IsNullOrEmpty(transactionId)) return;
            if (_ids.Add(transactionId) && UsePersistence) Save();
        }

        public int Count => _ids.Count;

        public void Clear()
        {
            _ids.Clear();
            if (UsePersistence) PlayerPrefs.DeleteKey(PrefsKey);
        }

        private void Save() => PlayerPrefs.SetString(PrefsKey, string.Join(",", _ids));

        private void Load()
        {
            string raw = PlayerPrefs.GetString(PrefsKey, string.Empty);
            if (string.IsNullOrEmpty(raw)) return;
            foreach (string id in raw.Split(','))
                if (id.Length > 0) _ids.Add(id);
        }
    }
}
