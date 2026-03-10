import { useState } from 'react'

const TEMPLATE_HEADER = 'schema_name,table_name,column_name,data_type'

const SAMPLE_DATA = `schema_name,table_name,column_name,data_type
public,users,id,uuid
public,users,email,text
public,users,created_at,timestamptz
public,users,is_active,boolean
public,orders,id,uuid
public,orders,user_id,uuid
public,orders,total_amount,numeric
public,orders,placed_at,timestamptz
public,orders,status,text
analytics,page_views,id,uuid
analytics,page_views,user_id,uuid
analytics,page_views,url,text
analytics,page_views,viewed_at,timestamptz`

const SQL: Record<string, string> = {
  PostgreSQL: `SELECT
  table_schema AS schema_name,
  table_name,
  column_name,
  data_type
FROM information_schema.columns
WHERE table_schema NOT IN ('pg_catalog', 'information_schema')
ORDER BY table_schema, table_name, ordinal_position;`,

  'SQL Server': `SELECT
  TABLE_SCHEMA AS schema_name,
  TABLE_NAME   AS table_name,
  COLUMN_NAME  AS column_name,
  DATA_TYPE    AS data_type
FROM INFORMATION_SCHEMA.COLUMNS
ORDER BY TABLE_SCHEMA, TABLE_NAME, ORDINAL_POSITION;`,

  MySQL: `SELECT
  TABLE_SCHEMA AS schema_name,
  TABLE_NAME   AS table_name,
  COLUMN_NAME  AS column_name,
  DATA_TYPE    AS data_type
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA NOT IN (
  'information_schema', 'mysql', 'performance_schema', 'sys'
)
ORDER BY TABLE_SCHEMA, TABLE_NAME, ORDINAL_POSITION;`,
}

const DB_TABS = Object.keys(SQL)

function downloadTemplate() {
  const blob = new Blob([TEMPLATE_HEADER + '\n'], { type: 'text/csv' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = 'datamap-template.csv'
  a.click()
  URL.revokeObjectURL(url)
}

export default function CsvUploadGuidePage() {
  const [activeDb, setActiveDb] = useState(DB_TABS[0])

  return (
    <div className="max-w-3xl mx-auto px-6 py-10 space-y-10">
      <div>
        <h1 className="text-2xl font-semibold text-gray-900 mb-1">CSV Upload Guide</h1>
        <p className="text-sm text-gray-500">
          Everything you need to prepare and upload your column catalog.
        </p>
      </div>

      {/* Required columns */}
      <section className="space-y-3">
        <h2 className="text-base font-semibold text-gray-800">Required columns</h2>
        <p className="text-sm text-gray-600">
          Your CSV must include a header row with exactly these four columns, in any order:
        </p>
        <table className="w-full text-sm border border-gray-200 rounded overflow-hidden">
          <thead className="bg-gray-50 text-gray-700">
            <tr>
              <th className="text-left px-4 py-2 font-medium border-b border-gray-200">Column</th>
              <th className="text-left px-4 py-2 font-medium border-b border-gray-200">Description</th>
              <th className="text-left px-4 py-2 font-medium border-b border-gray-200">Example</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {[
              { col: 'schema_name', desc: 'Database schema the table belongs to', ex: 'public' },
              { col: 'table_name',  desc: 'Name of the table',                    ex: 'orders' },
              { col: 'column_name', desc: 'Name of the column',                   ex: 'placed_at' },
              { col: 'data_type',   desc: 'Column data type',                     ex: 'timestamptz' },
            ].map(({ col, desc, ex }) => (
              <tr key={col} className="bg-white">
                <td className="px-4 py-2 font-mono text-blue-700">{col}</td>
                <td className="px-4 py-2 text-gray-600">{desc}</td>
                <td className="px-4 py-2 font-mono text-gray-500">{ex}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>

      {/* Template download */}
      <section className="space-y-3">
        <h2 className="text-base font-semibold text-gray-800">Template</h2>
        <p className="text-sm text-gray-600">Download a blank template or copy the header row directly.</p>
        <button
          onClick={downloadTemplate}
          className="px-4 py-2 text-sm bg-blue-600 text-white rounded hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500"
        >
          Download template (.csv)
        </button>
        <pre className="mt-3 bg-gray-50 border border-gray-200 rounded px-4 py-3 text-sm font-mono text-gray-700 overflow-x-auto">
          {TEMPLATE_HEADER}
        </pre>
      </section>

      {/* Upload behavior */}
      <section className="space-y-2">
        <h2 className="text-base font-semibold text-gray-800">Upload behavior</h2>
        <ul className="text-sm text-gray-600 space-y-1 list-disc list-inside">
          <li>Uploads are <strong>upserts</strong> — re-uploading the same data will not create duplicates.</li>
          <li>New schemas, tables, and columns are created automatically.</li>
          <li>Existing columns are updated if their data type changes.</li>
          <li>Every change is logged in the audit trail.</li>
        </ul>
      </section>

      {/* SQL queries */}
      <section className="space-y-3">
        <h2 className="text-base font-semibold text-gray-800">Extract from your database</h2>
        <p className="text-sm text-gray-600">
          Run this query against your database and export the result as CSV.
        </p>
        <div>
          <div className="flex border-b border-gray-200">
            {DB_TABS.map(db => (
              <button
                key={db}
                onClick={() => setActiveDb(db)}
                className={`px-4 py-2 text-sm font-medium focus:outline-none ${
                  activeDb === db
                    ? 'border-b-2 border-blue-600 text-blue-600'
                    : 'text-gray-500 hover:text-gray-700'
                }`}
              >
                {db}
              </button>
            ))}
          </div>
          <pre className="bg-gray-50 border border-t-0 border-gray-200 rounded-b px-4 py-3 text-sm font-mono text-gray-700 overflow-x-auto">
            {SQL[activeDb]}
          </pre>
        </div>
      </section>

      {/* Sample data */}
      <section className="space-y-3">
        <h2 className="text-base font-semibold text-gray-800">Sample data</h2>
        <pre className="bg-gray-50 border border-gray-200 rounded px-4 py-3 text-sm font-mono text-gray-700 overflow-x-auto">
          {SAMPLE_DATA}
        </pre>
      </section>
    </div>
  )
}
