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

        /// <summary>
        /// 各樓層站內 TaskType 對照表 (格式: "1F:F002,2F:F001,3F:F001,...")
        /// 未設定的樓層 fallback 使用 AGVTaskType
        /// </summary>
        [ConfigurationProperty("SameFloorTaskTypeMap", DefaultValue = "")]
        public string SameFloorTaskTypeMap
        {
            get
            {
                return (string)this["SameFloorTaskTypeMap"];
            }
            private set
            {
                this["SameFloorTaskTypeMap"] = value;
            }
        }

        /// <summary>
        /// 跨樓層 TaskType 對照表 (格式: "1F>3F:F13Test,3F>1F:F31Test,...")
        /// </summary>
        [ConfigurationProperty("CrossFloorTaskTypeMap", DefaultValue = "1F>3F:F13Test,3F>1F:F31Test,3F>4F:F34Test,4F>3F:F43Test,2F>4F:F24Test,4F>2F:F42Test")]
        public string CrossFloorTaskTypeMap
        {
            get
            {
                return (string)this["CrossFloorTaskTypeMap"];
            }
            private set
            {
                this["CrossFloorTaskTypeMap"] = value;
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
    }
}