using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace HikAGVDll
{
    public class SectionAGV : ConfigurationSection
    {
        [ConfigurationProperty("AGVUrlSettings")]
        public AGVUrlSettings AGVUrlSettings
        {
            get
            {
                return (AGVUrlSettings)this["AGVUrlSettings"];
            }
            set
            {
                this["AGVUrlSettings"] = value;
            }
        }

        [ConfigurationProperty("AGVSettings")]
        public AGVSettings AGVSettings
        {
            get
            {
                return (AGVSettings)this["AGVSettings"];
            }
            set
            {
                this["AGVSettings"] = value;
            }
        }
    }

    public class AGVUrlSettings : ConfigurationElement
    {
        [ConfigurationProperty("RestURL", DefaultValue = "")]
        public string RestURL
        {
            get
            {
                return (string)this["RestURL"];
            }
            private set
            {
                this["RestURL"] = value;
            }
        }

        [ConfigurationProperty("AGVStatusURL", DefaultValue = "")]
        public string AGVStatusURL
        {
            get
            {
                return (string)this["AGVStatusURL"];
            }
            private set
            {
                this["AGVStatusURL"] = value;
            }
        }

        [ConfigurationProperty("ContentType", DefaultValue = "application/json")]
        public string ContentType
        {
            get
            {
                return (string)this["ContentType"];
            }
            private set
            {
                this["ContentType"] = value;
            }
        }

        [ConfigurationProperty("CallBackURL", DefaultValue = "http://localhost:54632/agv/agvCallbackService/{0}")]
        internal string CallBackURL
        {
            get
            {
                return (string)this["CallBackURL"];
            }
            private set
            {
                this["CallBackURL"] = value;
            }
        }
    }

    public class AGVSettings : ConfigurationElement
    {
        [ConfigurationProperty("AGVMapCode", DefaultValue = "")]
        public string AGVMapCode
        {
            get
            {
                return (string)this["AGVMapCode"];
            }
            private set
            {
                this["AGVMapCode"] = value;
            }
        }

        [ConfigurationProperty("AGVTaskType", DefaultValue = "")]
        public string AGVTaskType
        {
            get
            {
                return (string)this["AGVTaskType"];
            }
            private set
            {
                this["AGVTaskType"] = value;
            }
        }

        [ConfigurationProperty("WarnContent", DefaultValue = "安全告警-前碰撞条触发,安全告警-后碰撞条触发")]
        public string WarnContent
        {
            get
            {
                return (string)this["WarnContent"];
            }
            private set
            {
                this["WarnContent"] = value;
            }
        }

        /// <summary>
        /// 跨樓層車輛編號（歸位機制與預調度機制共用）
        /// </summary>
        [ConfigurationProperty("CrossFloorShuttleId", DefaultValue = "1")]
        public string CrossFloorShuttleId
        {
            get
            {
                return (string)this["CrossFloorShuttleId"];
            }
            private set
            {
                this["CrossFloorShuttleId"] = value;
            }
        }

        /// <summary>
        /// 跨樓層車輛閒置歸位超時秒數（預設 300 秒）
        /// </summary>
        [ConfigurationProperty("IdleReturnTimeout", DefaultValue = 300)]
        public int IdleReturnTimeout
        {
            get
            {
                return (int)this["IdleReturnTimeout"];
            }
            private set
            {
                this["IdleReturnTimeout"] = value;
            }
        }

        /// <summary>
        /// 跨樓層車輛歸位目的地樓層（預設 4F）
        /// </summary>
        [ConfigurationProperty("IdleReturnFloor", DefaultValue = "4F")]
        public string IdleReturnFloor
        {
            get
            {
                return (string)this["IdleReturnFloor"];
            }
            private set
            {
                this["IdleReturnFloor"] = value;
            }
        }

        /// <summary>
        /// MapCode 對應樓層（格式：MapCode:Floor,MapCode:Floor）
        /// </summary>
        [ConfigurationProperty("MapCodeFloorMapping", DefaultValue = "AA:1F,BB:2F,DD:3F,FF:4F")]
        public string MapCodeFloorMapping
        {
            get
            {
                return (string)this["MapCodeFloorMapping"];
            }
            private set
            {
                this["MapCodeFloorMapping"] = value;
            }
        }
    }
}