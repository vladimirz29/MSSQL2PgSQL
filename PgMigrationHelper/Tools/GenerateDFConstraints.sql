SELECT 'alter table "'+SCHEMA_NAME(t.schema_id)+'"."'+t.[name]+'"
        alter column "'+col.[name]+'" set default '+
       (CASE WHEN col.system_type_id = 104 THEN
                 CASE WHEN LEFT(REPLACE(con.[definition],'(',''),1) = '1' THEN 'true' ELSE 'false' END
             WHEN col.system_type_id IN (48, 52, 56, 59, 60, 62, 106, 108, 122, 127) THEN
                 CASE WHEN LEFT(REPLACE(con.[definition],'(',''),2) = '''''' THEN '0' ELSE con.[definition] END
             ELSE
                 REPLACE(con.[definition], 'getdate()','now()')
           END) + ';' AS [Output]
FROM sys.default_constraints con
         LEFT OUTER JOIN sys.objects t
                         ON con.parent_object_id = t.object_id
         LEFT OUTER JOIN sys.all_columns col
                         ON con.parent_column_id = col.column_id
                             AND con.parent_object_id = col.object_id
WHERE SCHEMA_NAME(t.schema_id) + '.' + t.[name] NOT IN ({0})
