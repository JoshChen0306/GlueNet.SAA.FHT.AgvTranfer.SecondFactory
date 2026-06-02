using System;
using System.Data;
using System.Data.SqlClient;

namespace svrPair.Database
{
    /// <summary>
    /// SQL Server 資料庫連線輔助類別
    /// 解決原 MsSql 類別硬編碼連線字串的問題
    /// </summary>
    public class SqlHelper
    {
        private readonly string _connectionString;
        private readonly object _sqlWriteLock = new object();
        private readonly object _sqlReadLock = new object();
        private const int MaxRetryCount = 2;
        private const int CommandTimeout = 1200;

        #region [建構子]
        
        /// <summary>
        /// 使用完整連線字串建立 SqlHelper
        /// </summary>
        /// <param name="connectionString">完整的 SQL Server 連線字串</param>
        public SqlHelper(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("連線字串不可為空", nameof(connectionString));
            }
            
            _connectionString = connectionString;
        }

        /// <summary>
        /// 使用個別參數建立 SqlHelper (相容於原 MsSql 類別)
        /// </summary>
        /// <param name="server">伺服器位置 (例: DESKTOP-2I3FKA2\SQLEXPRESS)</param>
        /// <param name="database">資料庫名稱</param>
        /// <param name="userId">使用者帳號</param>
        /// <param name="password">密碼</param>
        /// <param name="encrypt">是否加密連線</param>
        /// <param name="trustServerCertificate">是否信任伺服器憑證</param>
        public SqlHelper(
            string server, 
            string database, 
            string userId = "mcs", 
            string password = "Zz123456",
            bool encrypt = false,
            bool trustServerCertificate = true)
        {
            if (string.IsNullOrWhiteSpace(server))
            {
                throw new ArgumentException("伺服器位置不可為空", nameof(server));
            }
            
            if (string.IsNullOrWhiteSpace(database))
            {
                throw new ArgumentException("資料庫名稱不可為空", nameof(database));
            }

            _connectionString = BuildConnectionString(
                server, 
                database, 
                userId, 
                password, 
                encrypt, 
                trustServerCertificate
            );
        }

        #endregion

        #region [連線字串建立]

        /// <summary>
        /// 建立 SQL Server 連線字串
        /// </summary>
        private static string BuildConnectionString(
            string server,
            string database,
            string userId,
            string password,
            bool encrypt,
            bool trustServerCertificate)
        {
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = server,
                InitialCatalog = database,
                UserID = userId,
                Password = password,
                PersistSecurityInfo = true,
                Encrypt = encrypt,
                TrustServerCertificate = trustServerCertificate
            };

            return builder.ConnectionString;
        }

        #endregion

        #region [寫入操作 - 相容於原 WriteSqlByAutoOpen]

        /// <summary>
        /// 執行 SQL 寫入指令 (INSERT, UPDATE, DELETE)
        /// </summary>
        /// <param name="sqlCommand">SQL 指令</param>
        public void WriteSqlByAutoOpen(string sqlCommand)
        {
            if (string.IsNullOrWhiteSpace(sqlCommand))
            {
                throw new ArgumentException("SQL 指令不可為空", nameof(sqlCommand));
            }

            lock (_sqlWriteLock)
            {
                Exception lastException = null;

                for (int attempt = 1; attempt <= MaxRetryCount; attempt++)
                {
                    using (var connection = new SqlConnection(_connectionString))
                    {
                        try
                        {
                            connection.Open();

                            using (var command = new SqlCommand(sqlCommand, connection))
                            {
                                command.ExecuteNonQuery();
                            }

                            return; // 成功執行,結束方法
                        }
                        catch (Exception ex)
                        {
                            lastException = ex;
                            
                            // 記錄重試資訊 (可選)
                            if (attempt < MaxRetryCount)
                            {
                                System.Diagnostics.Debug.WriteLine(
                                    $"SQL 寫入失敗,第 {attempt} 次重試: {ex.Message}"
                                );
                            }
                        }
                    }
                }

                // 所有重試都失敗
                throw new Exception($"SQL 寫入操作失敗: {lastException?.Message}", lastException);
            }
        }

        #endregion

        #region [查詢操作 - 相容於原 QuerySqlByAutoOpen]

        /// <summary>
        /// 執行 SQL 查詢指令 (SELECT)
        /// </summary>
        /// <param name="sqlQuery">SQL 查詢指令</param>
        /// <returns>包含查詢結果的 DataSet</returns>
        public DataSet QuerySqlByAutoOpen(string sqlQuery)
        {
            if (string.IsNullOrWhiteSpace(sqlQuery))
            {
                throw new ArgumentException("SQL 查詢不可為空", nameof(sqlQuery));
            }

            lock (_sqlReadLock)
            {
                Exception lastException = null;

                for (int attempt = 1; attempt <= MaxRetryCount; attempt++)
                {
                    using (var connection = new SqlConnection(_connectionString))
                    {
                        try
                        {
                            connection.Open();

                            using (var adapter = new SqlDataAdapter(sqlQuery, connection))
                            {
                                adapter.SelectCommand.CommandTimeout = CommandTimeout;
                                
                                var dataSet = new DataSet();
                                adapter.Fill(dataSet);
                                
                                return dataSet;
                            }
                        }
                        catch (Exception ex)
                        {
                            lastException = ex;
                            
                            // 記錄重試資訊 (可選)
                            if (attempt < MaxRetryCount)
                            {
                                System.Diagnostics.Debug.WriteLine(
                                    $"SQL 查詢失敗,第 {attempt} 次重試: {ex.Message}"
                                );
                            }
                        }
                    }
                }

                // 所有重試都失敗
                throw new Exception($"SQL 查詢操作失敗: {lastException?.Message}", lastException);
            }
        }

        #endregion

        #region [額外功能方法]

        /// <summary>
        /// 測試資料庫連線
        /// </summary>
        /// <returns>連線是否成功</returns>
        public bool TestConnection()
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 取得資料庫連線字串 (隱藏密碼)
        /// </summary>
        public string GetConnectionStringMasked()
        {
            var builder = new SqlConnectionStringBuilder(_connectionString);
            builder.Password = "****";
            return builder.ConnectionString;
        }

        #endregion
    }
}