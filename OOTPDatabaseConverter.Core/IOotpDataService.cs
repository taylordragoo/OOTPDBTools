#region File Description
//---------------------------------------------------------------------------
//
// File: IOotpDataService.cs
// Author: Claude
// Copyright: (C) 2024
// Description: Interface for OOTP Database data access operations.
//
//---------------------------------------------------------------------------
#endregion

#region License Info
//---------------------------------------------------------------------------
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 2 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.
//---------------------------------------------------------------------------
#endregion

namespace OOTPDatabaseConverter.Core
{
    /// <summary>
    /// Provides structured data access to OOTP database files.
    /// </summary>
    public interface IOotpDataService
    {
        /// <summary>
        /// Lists all available table names in the specified ODB file.
        /// </summary>
        /// <param name="odbPath">Path to the ODB file.</param>
        /// <returns>A collection of table names available in the database.</returns>
        /// <exception cref="FileNotFoundException">Thrown when the ODB file does not exist.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the ODB file cannot be read or parsed.</exception>
        Task<IEnumerable<string>> ListTablesAsync(string odbPath);

        /// <summary>
        /// Gets the column headers (schema) for a specific table.
        /// </summary>
        /// <param name="odbPath">Path to the ODB file.</param>
        /// <param name="tableName">Name of the table (without .csv extension).</param>
        /// <returns>A collection of column names for the specified table.</returns>
        /// <exception cref="FileNotFoundException">Thrown when the ODB file does not exist.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when the specified table is not found.</exception>
        Task<IEnumerable<string>> GetSchemaAsync(string odbPath, string tableName);

        /// <summary>
        /// Reads data from a specific table with pagination support.
        /// </summary>
        /// <param name="odbPath">Path to the ODB file.</param>
        /// <param name="tableName">Name of the table (without .csv extension).</param>
        /// <param name="offset">Number of rows to skip (default: 0).</param>
        /// <param name="limit">Maximum number of rows to return (default: 1000).</param>
        /// <returns>A collection of dictionaries where each dictionary represents a row with column names as keys.</returns>
        /// <exception cref="FileNotFoundException">Thrown when the ODB file does not exist.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when the specified table is not found.</exception>
        Task<IEnumerable<Dictionary<string, string>>> ReadTableAsync(string odbPath, string tableName, int offset = 0, int limit = 1000);

        /// <summary>
        /// Detects the OOTP version of the specified ODB file.
        /// </summary>
        /// <param name="odbPath">Path to the ODB file.</param>
        /// <returns>The detected OdbVersion.</returns>
        /// <exception cref="FileNotFoundException">Thrown when the ODB file does not exist.</exception>
        Task<OdbVersion> DetectVersionAsync(string odbPath);

        /// <summary>
        /// Gets configuration information about the database.
        /// </summary>
        /// <param name="odbPath">Path to the ODB file.</param>
        /// <returns>A formatted string containing database configuration details.</returns>
        /// <exception cref="FileNotFoundException">Thrown when the ODB file does not exist.</exception>
        Task<string> GetConfigAsync(string odbPath);

        /// <summary>
        /// Clears any cached data for the specified ODB file.
        /// </summary>
        /// <param name="odbPath">Path to the ODB file. If null, clears all cached data.</param>
        Task ClearCacheAsync(string? odbPath = null);
    }
}