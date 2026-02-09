using System.Collections.Generic;
using UnityEngine;

public class EventData
{
    public string EventName { get; }
    public string ParamValue { get; }
    public Dictionary<string, object> Attributes { get; }

    private EventData(Builder builder)
    {
        EventName = builder.EventName;
        ParamValue = builder.ParamValue;
        Attributes = builder.Attributes;
    }

    public class Builder
    {
        public string EventName { get; private set; }
        public string ParamValue { get; private set; }
        public Dictionary<string, object> Attributes { get; private set; }

        public Builder(string eventName)
        {
            EventName = eventName;
            Attributes = new();
        }
        
        // ParamValue 설정
        public Builder SetParamValue(string paramValue)
        {
            ParamValue = paramValue;
            return this;
        }

        // Attribute 요소 추가
        public Builder AddAttribute(string key, object value)
        {
            if (Attributes == null) Attributes = new();
            Attributes[key] = value;
            return this;
        }
        
        // Attribute 덮어쓰기
        public Builder SetAttributes(Dictionary<string, object> attributes)
        {
            Attributes = attributes;
            return this;
        }

        public EventData Build() => new EventData(this);
    }
}
