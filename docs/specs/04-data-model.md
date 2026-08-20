[← Index](../specs.md)

## 1. Database Schema

**workspaces**
- id
- name
- created_at
- is_template — marks a workspace as a copy source for template invites
- source_template_id — nullable; set on workspaces created by `WorkspaceCopyService`, pointing back at the template they were copied from

**invites**
- id
- workspace_id
- token
- created_at
- expires_at
- max_uses
- used_count
- template_workspace_id — nullable; presence makes this a template invite (see [§3, Auth & Invites](03-auth-and-invites.md#3-participant-upsert-and-invite-usage-rules))

**participants**
- id
- workspace_id
- email
- invite_id
- created_at
- last_seen_at

Unique constraint: `(workspace_id, email)`

**participant_sessions**
- id
- participant_id
- workspace_id
- created_at
- last_seen_at

**schemas**
- id
- workspace_id
- name

**tables**
- id
- workspace_id
- schema_id
- name
- description
- created_at
- updated_at

**columns**
- id
- workspace_id
- table_id
- name
- data_type
- example_value
- description
- owner
- version
- created_at
- updated_at

Column `version` is used for optimistic concurrency control. `PATCH` requests must include the current version. If a version mismatch occurs → return `409 Conflict`.

**relationships**
- id
- workspace_id
- source_column_id
- target_column_id
- relationship_type

**business_terms**
- id
- workspace_id
- name
- definition

**term_column_mapping**
- id
- term_id
- column_id

**metadata_changes**
- id
- entity_type
- entity_id
- field
- old_value
- new_value
- participant_id
- edited_at

## 2. Projection Table (Read Model)

**Table name:** `column_catalog_editor`

**Purpose:** provide fast spreadsheet grid queries. This table is NOT a materialized view — it is a writable projection table maintained by the application.

**Fields**
- workspace_id
- column_id
- schema_name
- table_name
- column_name
- data_type
- example_value
- description
- business_term
- owner
- version
