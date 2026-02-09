using System;
using System.Reflection;
using System.Runtime.Serialization;
using UnityEngine;

namespace Blossom.Preference {
    
    public interface IPrefData {
        string Key { get; set; }
        void Flush();
        void Clear();
        void OnChanged();
    }
    
    [Serializable]
    public class PrefData : IPrefData {
        public string Key {
            get => _key;
            set => _key = value;
        }
        [NonSerialized] private string _key;

        public PrefData() {
            _key = this.GetType().Name;
            RebindPrefValues();
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context) {
            _key = this.GetType().Name;
            RebindPrefValues();
        }

        protected void RebindPrefValues() {
            const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (FieldInfo fieldInfo in GetType().GetFields(FLAGS)) 
            {
                Type t = fieldInfo.FieldType;
                if (!t.IsGenericType) continue;
                if (t.GetGenericTypeDefinition() != typeof(PrefValue<>)) continue;
                object fieldInfoValue = fieldInfo.GetValue(this);
                if (fieldInfoValue == null) continue;

                FieldInfo parentField = t.GetField("_parent", FLAGS);
                parentField?.SetValue(fieldInfoValue, this);

                FieldInfo valueField = t.GetField("_value", FLAGS);
                FieldInfo displayValueField = t.GetField("_displayValue", FLAGS);
                if (valueField != null && displayValueField != null) {
                    displayValueField.SetValue(fieldInfoValue, valueField.GetValue(fieldInfoValue));
                }
            }
        }

        /// <summary>
        /// 저장할떄 쓰는거 
        /// </summary>
        public virtual void Flush() { }
        
        /// <summary>
        /// 데이터 초기화 할 때 쓰는거
        /// </summary>
        public virtual void Clear() { }

        public virtual void OnChanged() 
        {
            PrefSystem.OnChanged(_key);
        }
    }
}
