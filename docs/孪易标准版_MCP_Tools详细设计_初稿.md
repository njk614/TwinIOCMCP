# 孪易标准版 MCP Tools 详细设计初稿

> 设计日期：2026-07-14  
> 参考资料：`input/260713 会议纪要01（孪易标准版需求问题会）.md`、`input/场景自动生成系统_MCP_Tools详细设计.md`、`input/孪易 标准版 对象MCP.xlsx` 的 `20260713` 页签  
> 设计定位：孪易标准版执行型 MCP Server  
> 当前目标：先定义 MCP 粒子对象工具，不展开 Skill 和 Agent 编排。

## 1. 设计结论

孪易标准版 MCP 的核心目标，是把孪易后端现有可配置能力封装成 Agent 可调用的粒子化 MCP Tools。MCP 层只负责稳定、可追踪、安全地调用后端接口，不直接替代 Skill 的业务编排，也不承担自然语言规划。

孪易后端配置分为两个主域：

| 主域 | 说明 | 典型对象 |
| --- | --- | --- |
| 租户域 | 一个租户下资源公用，可创建多个场景 | 资源资产、资源管理、租户级孪生体类别 |
| 场景域 | 场景内的空间结构和实例化对象 | 场景、层级、点线面、孪生体实例、图层、图表、告警等 |

MCP Tool 设计采用“按对象拆分”的粒子化方式。上层 Skill 或 Agent 负责组合这些工具完成业务闭环。

## 2. 分期范围

### 2.1 P0 范围

P0 聚焦“创建一个可承载孪生体实例的基础场景闭环”：

```text
登录获取 Token
  -> 创建或定位租户下场景
  -> 创建场景层级
  -> 创建点、线、面空间对象
  -> 创建或查询租户级孪生体类别
  -> 上传或选择租户资源管理中的模型/图片/媒体资源
  -> 创建孪生体实例并绑定类别、资源和空间位置
  -> 查询场景结构和实例结果
```

P0 对象：

| 对象域 | 对象 | 说明 |
| --- | --- | --- |
| 认证 | 登录会话 | 模拟登录孪易后端，维护接口 Token |
| 租户 | 租户信息 | 不支持跨租户，MCP 在当前登录租户内操作 |
| 场景 | 场景 | 创建、编辑、删除、查询、复制、导入、导出 |
| 场景 | 场景层级 | 场景树或层级节点 |
| 场景 | 兴趣点位 POI | 点对象 |
| 场景 | 指引路线 | 线对象 |
| 场景 | 重点区域 | 面对象 |
| 租户 | 孪生体类别 | 租户级类别模板，核心 ID 为 `twinCategoryID` |
| 场景 | 孪生体类别配置 | 租户类别加入场景后的配置，核心 ID 为 `twinCategoryConfigID`，实例必须归属这一层 |
| 租户 | 台账字段 | 孪生体类别绑定的台账字段 |
| 租户 | 时序字段 | 孪生体类别绑定的时序字段 |
| 租户 | 事件字段 | 孪生体类别绑定的事件字段 |
| 场景 | 孪生体实例 | 场景中可显示的孪生体对象实例 |
| 租户 | 资源管理 | 可上传、添加、编辑、删除的模型、图片、视频、媒体、数据表资源 |

### 2.2 P1 范围

P1 聚焦“数据和可视化应用搭建闭环”：

```text
创建或定位数据对象
  -> 配置对象透视表
  -> 创建图层和图表
  -> 配置业务主题
  -> 配置告警和视频服务
  -> 将图层、图表、告警、视频挂入场景或业务主题
```

P1 对象：

| 对象域 | 对象 | 说明 |
| --- | --- | --- |
| 数据 | 数据对象类别 | 与孪生体类别并行，主要服务图表、图层显示 |
| 数据 | 字段定义 | 数据对象字段 |
| 数据 | 数据对象数据 | 数据对象中的具体数据 |
| 数据 | 对象透视表 | 来源为孪生体数据或数据对象数据的组合整理结果 |
| 分析 | 图层文件夹、图层 | 可视化图层 |
| 分析 | 图表文件夹、图表 | 可视化图表 |
| 分析 | 业务主题 | 包含对象、图层、图表 |
| 告警 | 告警文件夹、告警规则条件 | 告警配置 |
| 告警 | 告警级别配置 | 告警等级 |
| 告警 | 告警数据 | 静态数据、IVS 数据等 |
| 视频 | 视频服务、视频资源 | IVS 剥离和自研视频改造暂不作为 P1 强制目标 |
| 资源 | 资源、资产引用 | 与 P0 资源管理衔接 |

### 2.3 P2 范围

P2 覆盖剩余能力和高级配置：

| 对象域 | 对象 |
| --- | --- |
| 过滤器 | 过滤文件夹、过滤器、过滤项 |
| 首页 | 首页配置 |
| 睿司 | 睿司指令控制、睿司面板配置 |
| 数据集成 | 结构化数据库、数据接口、网关、数据字典、同步管理 |
| 用户 | 用户信息、密码管理 |
| 场景显示 | 显示配置、导览、导览步骤、场景工具配置 |
| 高级资源 | 公共/社区资产、粒子资源、材质资源等固定资产 |

资源资产属于租户固定资产，P0/P1 先不作为可变更对象处理。

## 3. MCP 总体架构

```text
Agent / MCP Client
  -> TwinIOC.McpServer
      -> TwinIOCBackendClient
          -> 孪易标准版 Web API
              -> 业务数据库
              -> 文件/资源服务
              -> 场景配置服务
```

MCP Server 职责：

| 模块 | 职责 |
| --- | --- |
| `TwinIOCBackendSessionManager` | 登录、Token 缓存、Token 失效后重登 |
| `TwinIOCBackendClient` | 封装孪易后端 API 调用、错误映射、上传下载 |
| `TenantToolService` | 租户级对象查询和资源域适配 |
| `SceneToolService` | 场景、层级、点线面对象 CRUD |
| `TwinCategoryToolService` | 租户级孪生体类别、字段和模型配置 |
| `TwinInstanceToolService` | 场景内孪生体实例 CRUD |
| `ResourceToolService` | 资源管理中的模型、图片、视频、媒体、数据表资源 |
| `DataObjectToolService` | P1 数据对象和对象透视表 |
| `VisualizationToolService` | P1 图层、图表、业务主题 |
| `AlarmVideoToolService` | P1 告警、视频服务 |

## 4. 统一返回模型

所有工具统一返回：

```json
{
  "status": "success",
  "summary": "操作成功",
  "data": {},
  "warnings": [],
  "failed_items": [],
  "affected_objects": [],
  "next_actions": []
}
```

| 字段 | 说明 |
| --- | --- |
| `status` | `success`、`failed`、`partial`、`accepted` |
| `summary` | 给 Agent 和用户看的摘要 |
| `data` | 工具返回主体 |
| `warnings` | 风险提示、缺失配置、删除确认提示 |
| `failed_items` | 批量操作失败项 |
| `affected_objects` | 被创建、修改、删除或绑定的对象 |
| `next_actions` | 推荐下一步可调用工具 |

## 5. 安全写入规则

所有写入工具支持 `preview` 和 `execute` 两种模式。

```text
preview:
  校验参数
  读取原对象
  返回 diff 和风险提示
  不写后端

execute:
  校验 confirm=true
  重新读取原对象
  合并变更
  调用后端接口
  返回 affected_objects
```

删除类操作 P0 不禁用，但必须给出影响范围和风险提示。执行删除必须显式传入 `confirm=true`。

孪易后端接口需要登录 Token。MCP Server 以当前配置的服务账号或用户账号登录，不支持跨租户操作。

## 6. P0 Tools 设计

### 6.1 认证与上下文

| Tool | 能力 | 说明 |
| --- | --- | --- |
| `get_twin_mcp_context` | 查看 MCP 上下文 | 返回 MCP 版本、当前租户、后端地址摘要、写入开关 |
| `login_twin_backend` | 登录孪易后端 | 获取并缓存 Token；通常由 MCP 内部自动调用 |
| `refresh_twin_backend_token` | 刷新 Token | Token 失效时重登 |
| `get_current_tenant` | 获取当前租户 | 返回当前登录租户信息，不支持跨租户 |

### 6.2 场景

| Tool | 能力 | 主要参数 |
| --- | --- | --- |
| `create_scene` | 创建场景 | `name`、`tenant_id?`、`description?`、`mode`、`confirm` |
| `list_scenes` | 查询场景列表 | `keyword?`、`page?`、`page_size?` |
| `get_scene` | 查询场景详情 | `scene_id` |
| `update_scene` | 编辑场景 | `scene_id`、`patch`、`mode`、`confirm` |
| `delete_scene` | 删除场景 | `scene_id`、`mode`、`confirm` |
| `copy_scene` | 复制场景 | `source_scene_id`、`target_name`、`mode`、`confirm` |
| `import_scene` | 导入场景 | `resource_id/file_id`、`mode`、`confirm` |
| `export_scene` | 导出场景 | `scene_id` |

### 6.3 场景层级

| Tool | 能力 | 主要参数 |
| --- | --- | --- |
| `create_scene_hierarchy` | 创建场景层级 | `scene_id`、`parent_id?`、`name`、`order?` |
| `list_scene_hierarchies` | 查询层级树 | `scene_id` |
| `get_scene_hierarchy` | 查询层级详情 | `hierarchy_id` |
| `update_scene_hierarchy` | 编辑层级 | `hierarchy_id`、`patch`、`mode`、`confirm` |
| `delete_scene_hierarchy` | 删除层级 | `hierarchy_id`、`mode`、`confirm` |
| `move_scene_hierarchy` | 移动层级 | `hierarchy_id`、`target_parent_id`、`order?` |
| `rename_scene_hierarchy` | 重命名层级 | `hierarchy_id`、`name` |

### 6.4 点线面空间对象

| Tool | 能力 | 对象 |
| --- | --- | --- |
| `create_scene_poi` | 创建兴趣点位 | 点 |
| `list_scene_pois` | 查询兴趣点位 | 点 |
| `get_scene_poi` | 查询兴趣点详情 | 点 |
| `update_scene_poi` | 编辑兴趣点位 | 点 |
| `delete_scene_poi` | 删除兴趣点位 | 点 |
| `create_guide_route` | 创建指引路线 | 线 |
| `list_guide_routes` | 查询指引路线 | 线 |
| `get_guide_route` | 查询路线详情 | 线 |
| `update_guide_route` | 编辑指引路线 | 线 |
| `delete_guide_route` | 删除指引路线 | 线 |
| `create_key_area` | 创建重点区域 | 面 |
| `list_key_areas` | 查询重点区域 | 面 |
| `get_key_area` | 查询区域详情 | 面 |
| `update_key_area` | 编辑重点区域 | 面 |
| `delete_key_area` | 删除重点区域 | 面 |

点线面对象统一支持：

| 参数 | 说明 |
| --- | --- |
| `scene_id` | 所属场景 |
| `hierarchy_id?` | 所属层级 |
| `name` | 对象名称 |
| `geometry` | 点、线、面坐标 |
| `style?` | 图标、颜色、宽度、透明度等显示样式 |
| `properties?` | 扩展业务属性 |

### 6.5 租户级孪生体类别

租户级孪生体类别是全租户共用的类别模板，核心 ID 是 `twinCategoryID`，不属于某个具体场景，也不带 `locationId`。它可以绑定默认台账表、时序表、事件表字段，但不能直接作为场景实例创建接口的类别 ID。

创建孪生体类别前必须先确定 `category_type`。MCP 应先暴露后端支持的类别类型枚举，再按类别类型读取默认字段，最后将默认字段与用户自定义字段合并后调用类别保存接口，避免创建出缺字段或类型不合法的类别。

| Tool | 能力 | 主要参数 |
| --- | --- | --- |
| `list_twin_category_types` | 查询可创建的类别元类型枚举，即 `category_type` 可选值，不是租户已创建列表 | 无 |
| `get_twin_category_default_fields` | 查询租户级类别类型默认字段 | `category_type` |
| `create_twin_category_folder` | 创建租户级类别文件夹 | `parent_id?`、`name` |
| `list_twin_category_folders` | 查询租户级类别文件夹；不传 `parent_id` 时自动用根 `folderID` 再查一次子文件夹接口 | `parent_id?` |
| `update_twin_category_folder` | 编辑租户级类别文件夹名称；禁止编辑根文件夹 | `folder_id`、`name`、`mode`、`confirm` |
| `rename_twin_category_folder` | 重命名租户级类别文件夹；`update_twin_category_folder` 的语义别名 | `folder_id`、`name`、`mode`、`confirm` |
| `delete_twin_category_folder` | 删除租户级类别文件夹；禁止删除根文件夹 | `folder_id`、`mode`、`confirm` |
| `list_tenant_twin_categories` | 查询租户已创建类别库；不传 `folder_id` 默认查 TwinCategory 根及所有子文件夹，且不按 `twinCategoryType` 过滤，避免漏掉 `DataObject` | `keyword?`、`folder_id?`、`include_all_folders?` |
| `list_twin_categories` | 查询租户已创建类别库的兼容入口；默认同样汇总根及所有子文件夹 | `keyword?`、`folder_id?`、`include_all_folders?` |
| `get_twin_category` | 查询租户级类别详情 | `category_id` = `twinCategoryID` |
| `create_twin_category` | 创建租户级类别模板 | `folder_id`、`name`、`category_type`、`ledger_fields?`、`time_series_fields?`、`event_fields?`、`model_config?` |
| `update_twin_category` | 编辑租户级类别模板 | `category_id`、`patch`、`mode`、`confirm` |
| `rename_twin_category` | 重命名租户级类别模板；`update_twin_category` 的轻量入口 | `category_id`、`name`、`mode`、`confirm` |
| `delete_twin_category` | 删除租户级类别模板 | `category_ids`、`mode`、`confirm` |
| `copy_twin_category` | 复制租户级类别模板；复制接口请求体是 `string[]` | `category_ids`、`target_folder_id?`、`mode`、`confirm` |
| `move_twin_category` | 移动租户级类别模板 | `category_ids`、`target_folder_id`、`mode`、`confirm` |

### 6.5.1 场景级孪生体类别配置

场景级孪生体类别配置是“某个租户级类别在某个场景内启用后的配置”，核心 ID 是 `twinCategoryConfigID`，必须带 `locationId`。后续创建孪生体实例、查询实例数据、编辑模型显示、读写台账/时序/事件数据，都应使用 `locationId + twinCategoryConfigID`，不能直接用租户级 `twinCategoryID` 替代。

| Tool | 能力 | 主要参数 |
| --- | --- | --- |
| `list_scene_twin_category_folders` | 查询场景级类别文件夹 | `scene_id`、`parent_folder_id?` |
| `create_scene_twin_category_folder` | 创建场景级类别文件夹 | `scene_id`、`parent_folder_id?`、`name` |
| `update_scene_twin_category_folder` | 编辑场景级类别文件夹名称 | `scene_id`、`folder_id`、`name`、`mode`、`confirm` |
| `rename_scene_twin_category_folder` | 重命名场景级类别文件夹；`update_scene_twin_category_folder` 的语义别名 | `scene_id`、`folder_id`、`name`、`mode`、`confirm` |
| `delete_scene_twin_category_folder` | 删除场景级类别文件夹 | `scene_id`、`folder_id`、`mode`、`confirm` |
| `list_scene_twin_categories` | 查询场景文件夹下已启用类别，返回 `twinCategoryConfigID` | `scene_id`、`folder_id`、`level_id?`、`region_id?` |
| `add_scene_twin_category` | 将租户级类别加入场景并生成配置；台账数据粒度默认按年 | `scene_id`、`folder_id`、`tenant_category_id` = `twinCategoryID`、`granularity?=1`、`granularity_type?=Year` |
| `get_scene_twin_category` | 查询场景级类别配置详情，输入 `twinCategoryConfigID` | `scene_id`、`twin_category_config_id` |
| `update_scene_twin_category` | 编辑/重命名场景级类别配置名称 | `scene_id`、`twin_category_config_id`、`name`、`mode`、`confirm` |
| `rename_scene_twin_category` | 重命名场景级类别配置；`update_scene_twin_category` 的语义别名 | `scene_id`、`twin_category_config_id`、`name`、`mode`、`confirm` |
| `delete_scene_twin_category` | 从场景移除类别配置 | `scene_id`、`twin_category_config_id`、`mode`、`confirm` |

### 6.6 孪生体类别字段

| Tool | 能力 | 字段类型 |
| --- | --- | --- |
| `create_twin_ledger_field` | 创建台账字段 | 台账 |
| `list_twin_ledger_fields` | 查询台账字段 | 台账 |
| `update_twin_ledger_field` | 编辑台账字段 | 台账 |
| `delete_twin_ledger_field` | 删除台账字段 | 台账 |
| `create_twin_timeseries_field` | 创建时序字段 | 时序 |
| `list_twin_timeseries_fields` | 查询时序字段 | 时序 |
| `update_twin_timeseries_field` | 编辑时序字段 | 时序 |
| `delete_twin_timeseries_field` | 删除时序字段 | 时序 |
| `create_twin_event_field` | 创建事件字段 | 事件 |
| `list_twin_event_fields` | 查询事件字段 | 事件 |
| `update_twin_event_field` | 编辑事件字段 | 事件 |
| `delete_twin_event_field` | 删除事件字段 | 事件 |

字段参数建议统一：

| 参数 | 说明 |
| --- | --- |
| `category_id` | 所属孪生体类别 |
| `field_name` | 字段名 |
| `display_name` | 展示名 |
| `data_type` | 字段类型 |
| `unit?` | 单位 |
| `required?` | 是否必填 |
| `dictionary_ref?` | 字典引用 |

### 6.7 孪生体类别模型配置

| Tool | 能力 |
| --- | --- |
| `get_twin_category_model_config` | 查询模型配置 |
| `update_twin_category_model_config` | 修改模型配置 |
| `get_twin_category_base_setting` | 查询基础设置 |
| `update_twin_category_base_setting` | 修改基础设置 |
| `get_twin_category_selection_setting` | 查询对象选中配置 |
| `update_twin_category_selection_setting` | 修改对象选中配置 |
| `get_twin_category_label_setting` | 查询标签设置 |
| `update_twin_category_label_setting` | 修改标签设置 |
| `get_twin_category_visibility_setting` | 查询可见设置 |
| `update_twin_category_visibility_setting` | 修改可见设置 |
| `create_twin_joint_binding` | 创建关节绑定 |
| `update_twin_joint_binding` | 修改关节绑定 |
| `delete_twin_joint_binding` | 删除关节绑定 |
| `create_twin_animation_binding` | 创建动画绑定 |
| `update_twin_animation_binding` | 修改动画绑定 |
| `delete_twin_animation_binding` | 删除动画绑定 |
| `create_twin_sensor_binding` | 创建传感器绑定 |
| `update_twin_sensor_binding` | 修改传感器绑定 |
| `delete_twin_sensor_binding` | 删除传感器绑定 |

### 6.8 场景孪生体实例

| Tool | 能力 | 主要参数 |
| --- | --- | --- |
| `create_twin_instance` | 创建孪生体实例 | `scene_id`、`twin_category_config_id`、`name`、`position?`、`resource_ref?` |
| `list_twin_instances` | 查询实例列表 | `scene_id`、`twin_category_config_id?`、`keyword?` |
| `get_twin_instance` | 查询实例详情 | `instance_id` |
| `update_twin_instance` | 编辑实例 | `instance_id`、`patch`、`mode`、`confirm` |
| `delete_twin_instance` | 删除实例 | `instance_id`、`mode`、`confirm` |
| `copy_twin_instance` | 复制实例 | `source_instance_id`、`target_name` |
| `move_twin_instance` | 移动实例 | `instance_id`、`target_hierarchy_id?`、`position?` |
| `bind_twin_instance_resource` | 绑定实例资源 | `instance_id`、`resource_id/resource_code` |
| `unbind_twin_instance_resource` | 解绑实例资源 | `instance_id`、`resource_id/resource_code` |
| `preview_twin_instance` | 预览实例 | `instance_id` |

实例数据工具：

| Tool | 能力 |
| --- | --- |
| `upsert_twin_instance_ledger_data` | 新增或更新实例台账数据 |
| `list_twin_instance_ledger_data` | 查询实例台账数据 |
| `delete_twin_instance_ledger_data` | 删除实例台账数据 |
| `upsert_twin_instance_timeseries_data` | 新增或更新实例时序数据 |
| `list_twin_instance_timeseries_data` | 查询实例时序数据 |
| `delete_twin_instance_timeseries_data` | 删除实例时序数据 |
| `upsert_twin_instance_event_data` | 新增或更新实例事件数据 |
| `list_twin_instance_event_data` | 查询实例事件数据 |
| `delete_twin_instance_event_data` | 删除实例事件数据 |

### 6.9 资源管理

资源管理是租户下可变更资源。资源资产属于固定资产库，P0 先不纳入可变更工具。

| Tool | 能力 | 对象 |
| --- | --- | --- |
| `create_resource_folder` | 创建资源文件夹 | 文件夹 |
| `list_resource_folders` | 查询资源文件夹 | 文件夹 |
| `update_resource_folder` | 编辑资源文件夹 | 文件夹 |
| `delete_resource_folder` | 删除资源文件夹 | 文件夹 |
| `begin_resource_upload` | 开始资源上传 | 模型、图片、视频、媒体、数据表 |
| `commit_resource_upload` | 完成资源上传 | 模型、图片、视频、媒体、数据表 |
| `cancel_resource_upload` | 取消资源上传 | 上传会话 |
| `list_resources` | 查询资源 | 模型、图片、视频、媒体、数据表 |
| `get_resource` | 查询资源详情 | 模型、图片、视频、媒体、数据表 |
| `update_resource` | 编辑资源元数据 | 模型、图片、视频、媒体、数据表 |
| `delete_resource` | 删除资源 | 模型、图片、视频、媒体、数据表 |
| `preview_resource` | 预览资源 | 模型、图片、视频、媒体、数据表 |
| `bind_resource` | 绑定资源 | 绑定到类别、实例或配置 |
| `unbind_resource` | 解绑资源 | 从类别、实例或配置解绑 |

`resource_code` / `asset_id` 是否统一，需要以孪易后端接口返回为准。MCP 层先定义统一抽象：

```json
{
  "resource_ref": {
    "resource_id": "backend-id",
    "resource_code": "optional-code",
    "asset_id": "optional-asset-id",
    "resource_type": "model|image|video|media|data_table",
    "raw": {}
  }
}
```

## 7. P0 Tool 详细规格（待后端接口校准）

本节先定义 MCP 层稳定契约。后端接口路径已根据 `input/后端API/backend-config-api.json` 和 `input/后端API/swagger.json` 做第一轮校准；DTO 字段名、请求体结构和少量上传/导入接口仍需要结合后端 Controller、Swagger Schema、前端调用代码或抓包结果继续校准。

后端接口通用约定：

| 项 | 说明 |
| --- | --- |
| API 前缀 | `/v1` |
| 鉴权 | `Authorization: Bearer {token}` |
| 登录 | `POST /v1/login` |
| 响应壳 | `{ code: number, msg: string, data: any }` |
| 成功码 | `code = 10000` |
| 操作域 | `{operationalData}`，常见为 `IndustryData` 或 `UserData` |

### 7.1 通用参数约定

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `mode` | string | 写入工具必填 | `preview` 或 `execute` |
| `confirm` | boolean | 删除、覆盖、批量写入必填 | `execute` 时高风险操作必须为 `true` |
| `expected_update_time` | string | 否 | 弱并发保护字段，后端无 revision 时用最后更新时间 |
| `request_id` | string | 否 | 幂等和排障追踪 ID |
| `page` | number | 列表工具可选 | 页码 |
| `page_size` | number | 列表工具可选 | 每页数量 |

### 7.2 通用出参约定

所有 P0 Tool 均返回统一结果壳：

```json
{
  "status": "success",
  "summary": "操作成功",
  "data": {},
  "warnings": [],
  "failed_items": [],
  "affected_objects": [],
  "next_actions": []
}
```

写入工具在 `preview` 模式下，`data` 应返回：

```json
{
  "mode": "preview",
  "diff": [],
  "risk_level": "low|medium|high",
  "requires_confirm": true
}
```

删除工具在 `preview` 模式下必须返回影响范围：

```json
{
  "delete_target": {},
  "dependency_summary": {},
  "will_delete": [],
  "will_unbind": [],
  "will_keep": []
}
```

### 7.3 认证与上下文 Tools

| Tool | 中文说明 | MCP 入参 | MCP 出参 `data` | 后端接口映射（待确认） |
| --- | --- | --- | --- | --- |
| `get_twin_mcp_context` | 获取 MCP 服务和当前后端连接上下文 | 无 | `mcp_version`、`backend_base_url`、`current_user`、`operational_data`、`write_tools_enabled` | MCP 本地配置 + `GET /v1/my/info` |
| `login_twin_backend` | 登录孪易后端并缓存 Token | `username`、`password`、`captcha?` | `access_token` 脱敏摘要、`user_id`、`user_name`、`user_type`、`is_admin` | `POST /v1/login` |
| `refresh_twin_backend_token` | 刷新或重新获取后端 Token | 无 | `token_expire_time?`、`refreshed` | 未发现独立刷新接口；默认重新调用 `POST /v1/login` |
| `get_current_tenant` | 获取当前用户和租户/操作域信息 | 无 | `operational_data`、`user_id`、`user_name`、`user_type`、`is_admin` | `GET /v1/my/info`；租户字段需结合用户信息或配置确认 |

实现要求：

1. MCP Server 内部调用后端接口时自动携带 Token。
2. 后端返回 401 或 Token 失效时，允许自动重登一次。
3. 返回值和日志不得输出完整 Token、密码、验证码。

### 7.4 场景 Tools

| Tool | 中文说明 | MCP 入参 | MCP 出参 `data` | 后端接口映射（待确认） |
| --- | --- | --- | --- | --- |
| `create_scene` | 创建一个新的场景/地点 | `operational_data`、`asset_folder_id`、`name`、`description?`、`cover_resource_ref?`、`mode`、`confirm` | `scene`、`affected_objects` | `POST /v1/{operationalData}/assetFolders/{assetFolderID}/location/add` |
| `list_scenes` | 查询场景/地点列表 | `operational_data`、`asset_folder_id`、`keyword?`、`status?`、`page?`、`page_size?` | `items`、`total?`、`page?`、`page_size?` | `GET /v1/{operationalData}/assetFolders/{assetFolderID}/location`；分页用 `POST /v1/{operationalData}/assetFolders/{assetFolderID}/location` |
| `get_scene` | 查询单个场景详情 | `operational_data`、`scene_id` | `scene`、`hierarchy_summary`、`instance_summary` | `GET /v1/{operationalData}/location/{locationId}` |
| `update_scene` | 修改场景设置、名称或缩略图 | `operational_data`、`scene_id`、`patch`、`mode`、`confirm`、`expected_update_time?` | `scene`、`diff?` | `POST /v1/{operationalData}/location/{locationId}/edit`；重命名用 `POST /v1/{operationalData}/location/{locationId}/rename`；缩略图用 `/thumbnail` |
| `delete_scene` | 删除场景/地点 | `operational_data`、`scene_id`、`mode`、`confirm` | `delete_result`、`dependency_summary` | `POST /v1/{operationalData}/location/{locationId}/delete` |
| `copy_scene` | 复制行业或模板场景到目标文件夹 | `source_scene_id`、`target_folder_id`、`target_name?`、`mode`、`confirm` | `source_scene_id`、`target_scene` | `POST /v1/copyIndustryData/location/{locationId}/folder/{folderId}` |
| `import_scene` | 导入场景配置包到指定文件夹 | `asset_folder_id`、`file_ref/file_upload_id`、`mode`、`confirm` | `scene_id?`、`import_result` | `POST /v1/assetFolders/{assetFolderID}/location/import`；更新已有场景包用 `POST /v1/location/{locationId}/import` |
| `export_scene` | 导出场景配置包 ZIP | `scene_id`、`mode?`、`confirm?`、`download?` | `export_result`、`download_api?`、`file_ref?` | `HEAD /v1/location/{locationId}/exportToZip` 获取导出信息；`POST /v1/location/{locationId}/exportToZip` 生成 ZIP；`POST /v1/location/{locationId}/getExportZip` 下载 ZIP |

`scene` 建议统一为：

```json
{
  "scene_id": "backend-id",
  "tenant_id": "tenant-id",
  "name": "场景名称",
  "description": "",
  "status": "enabled",
  "cover_resource_ref": {},
  "create_time": "",
  "update_time": "",
  "raw": {}
}
```

### 7.5 场景层级 Tools

| Tool | 中文说明 | MCP 入参 | MCP 出参 `data` | 后端接口映射（待确认） |
| --- | --- | --- | --- | --- |
| `create_scene_hierarchy` | 创建场景楼层/层级 | `operational_data`、`scene_id`、`name`、`height?`、`level_height?`、`order?`、`mode`、`confirm` | `hierarchy` | `POST /v1/{operationalData}/location/{locationId}/level/add` |
| `list_scene_hierarchies` | 查询场景层级列表 | `operational_data`、`scene_id`、`tree?` | `items` 或 `tree` | `GET /v1/{operationalData}/location/{locationId}/level` |
| `get_scene_hierarchy` | 查询单个层级详情 | `operational_data`、`scene_id`、`hierarchy_id` | `hierarchy`、`position_summary`、`path_summary`、`region_summary` | `GET /v1/{operationalData}/location/{locationId}/level` 后由 MCP 过滤；单详情接口未单独发现 |
| `update_scene_hierarchy` | 修改场景层级配置 | `operational_data`、`scene_id`、`hierarchy_id`、`patch`、`mode`、`confirm` | `hierarchy`、`diff?` | `POST /v1/{operationalData}/location/{locationId}/level/{levelId}/edit` |
| `delete_scene_hierarchy` | 删除场景层级 | `operational_data`、`scene_id`、`hierarchy_ids`、`mode`、`confirm` | `delete_result`、`dependency_summary` | `POST /v1/{operationalData}/location/{locationId}/level/delete` |
| `move_scene_hierarchy` | 调整层级顺序 | `operational_data`、`scene_id`、`hierarchy_id`、`sort_type`、`mode`、`confirm` | `hierarchy`、`sort_type` | `POST /v1/{operationalData}/location/{locationId}/level/{levelId}/{sortType}` |
| `rename_scene_hierarchy` | 重命名场景层级 | `operational_data`、`scene_id`、`hierarchy_id`、`name`、`mode`、`confirm` | `hierarchy` | 使用 `POST /v1/{operationalData}/location/{locationId}/level/{levelId}/edit` |

`hierarchy` 建议统一为：

```json
{
  "hierarchy_id": "backend-id",
  "scene_id": "scene-id",
  "parent_id": null,
  "name": "层级名称",
  "order": 1,
  "path": [],
  "raw": {}
}
```

### 7.6 点线面空间对象 Tools

点、线、面分为 POI、指引路线、重点区域三个对象。三者字段结构保持一致，只是 `geometry.type` 不同。

| Tool | 中文说明 | MCP 入参 | MCP 出参 `data` | 后端接口映射（待确认） |
| --- | --- | --- | --- | --- |
| `create_scene_poi` | 创建兴趣点位 POI | `operational_data`、`scene_id`、`hierarchy_id`、`name`、`geometry`、`style?`、`properties?`、`mode`、`confirm` | `spatial_object` | `POST /v1/{operationalData}/location/{locationId}/level/{levelId}/position/add` |
| `list_scene_pois` | 查询兴趣点位列表 | `operational_data`、`scene_id`、`hierarchy_id`、`keyword?` | `items` | `GET /v1/{operationalData}/location/{locationId}/level/{levelId}/position` |
| `get_scene_poi` | 查询单个兴趣点位详情 | `operational_data`、`scene_id`、`hierarchy_id`、`poi_id` | `spatial_object` | 列表接口后由 MCP 按 `positionID` 过滤 |
| `update_scene_poi` | 修改兴趣点位配置 | `operational_data`、`scene_id`、`poi_id`、`patch`、`mode`、`confirm` | `spatial_object`、`diff?` | `POST /v1/{operationalData}/location/{locationId}/position/{positionId}/edit` |
| `delete_scene_poi` | 删除兴趣点位 | `operational_data`、`scene_id`、`poi_ids`、`mode`、`confirm` | `delete_result` | `POST /v1/{operationalData}/location/{locationId}/position/delete` |
| `create_guide_route` | 创建指引路线 | `operational_data`、`scene_id`、`hierarchy_id`、`name`、`geometry`、`style?`、`properties?`、`mode`、`confirm` | `spatial_object` | `POST /v1/{operationalData}/location/{locationId}/level/{levelId}/path/add` |
| `list_guide_routes` | 查询指引路线列表 | `operational_data`、`scene_id`、`hierarchy_id`、`keyword?` | `items` | `GET /v1/{operationalData}/location/{locationId}/level/{levelId}/path` |
| `get_guide_route` | 查询单条指引路线详情 | `operational_data`、`scene_id`、`hierarchy_id`、`route_id` | `spatial_object` | 列表接口后由 MCP 按 `pathID` 过滤 |
| `update_guide_route` | 修改指引路线配置 | `operational_data`、`scene_id`、`route_id`、`patch`、`mode`、`confirm` | `spatial_object`、`diff?` | `POST /v1/{operationalData}/location/{locationId}/path/{pathId}/edit` |
| `delete_guide_route` | 删除指引路线 | `operational_data`、`scene_id`、`route_ids`、`mode`、`confirm` | `delete_result` | `POST /v1/{operationalData}/location/{locationId}/path/delete` |
| `create_key_area` | 创建重点区域 | `operational_data`、`scene_id`、`hierarchy_id`、`name`、`geometry`、`style?`、`properties?`、`mode`、`confirm` | `spatial_object` | `POST /v1/{operationalData}/location/{locationId}/level/{levelId}/region/add` |
| `list_key_areas` | 查询重点区域列表 | `operational_data`、`scene_id`、`hierarchy_id`、`keyword?` | `items` | `GET /v1/{operationalData}/location/{locationId}/level/{levelId}/region` |
| `get_key_area` | 查询单个重点区域详情 | `operational_data`、`scene_id`、`hierarchy_id`、`area_id` | `spatial_object` | 列表接口后由 MCP 按 `regionID` 过滤 |
| `update_key_area` | 修改重点区域配置 | `operational_data`、`scene_id`、`area_id`、`patch`、`mode`、`confirm` | `spatial_object`、`diff?` | `POST /v1/{operationalData}/location/{locationId}/region/{regionId}/edit` |
| `delete_key_area` | 删除重点区域 | `operational_data`、`scene_id`、`area_ids`、`mode`、`confirm` | `delete_result` | `POST /v1/{operationalData}/location/{locationId}/region/delete` |

`geometry` 建议采用 GeoJSON 风格，方便 Agent 和 GIS 数据对接：

```json
{
  "type": "Point|LineString|Polygon",
  "coordinates": [],
  "coordinate_system": "EPSG:4326"
}
```

`spatial_object` 建议统一为：

```json
{
  "object_id": "backend-id",
  "object_type": "poi|guide_route|key_area",
  "scene_id": "scene-id",
  "hierarchy_id": "hierarchy-id",
  "name": "对象名称",
  "geometry": {},
  "style": {},
  "properties": {},
  "raw": {}
}
```

### 7.7 租户级孪生体类别 Tools

| Tool | 中文说明 | MCP 入参 | MCP 出参 `data` | 后端接口映射（待确认） |
| --- | --- | --- | --- | --- |
| `list_twin_category_types` | 查询可创建的孪生体类别元类型枚举 | 无 | `items`、`total_available_count`、`returned_count` | 孪易标准版 MCP 内置 13 类可创建 `categoryType` 白名单：现有 12 类 + `DataObject`；部署不依赖 `input` 目录；不用于回答租户已创建类别有哪些 |
| `get_twin_category_default_fields` | 查询指定类别类型的默认字段 | `category_type` | `category_type`、`ledger_fields`、`time_series_fields`、`event_fields`、`raw` | `GET /v1/twinCategory/defaultField/{categoryType}` |
| `create_twin_category_folder` | 创建租户级孪生体类别文件夹 | `operational_data`、`parent_id`、`name`、`mode`、`confirm` | `folder` | `POST /v1/{operationalData}/{assetFolderType}/assetFolder/{parentFolderId}/add`，其中 `assetFolderType=TwinCategory` |
| `list_twin_category_folders` | 查询孪生体类别文件夹 | `operational_data`、`parent_id?`、`tree?` | `root_folder`、`items` 或 `tree` | 不传 `parent_id`：先 `GET /v1/{operationalData}/TwinCategory/assetFolder/root` 取根 `folderID`，再 `GET /v1/{operationalData}/TwinCategory/assetFolder/{parentFolderId}` 查根下子文件夹；传 `parent_id` 时直接查子级 |
| `update_twin_category_folder` | 编辑租户级孪生体类别文件夹名称 | `operational_data`、`folder_id`、`name`、`mode`、`confirm` | `folder`、`folder_id` | `POST /v1/{operationalData}/TwinCategory/assetFolder/{folderId}/edit`；禁止编辑 TwinCategory 根文件夹 |
| `rename_twin_category_folder` | 重命名租户级孪生体类别文件夹 | `operational_data`、`folder_id`、`name`、`mode`、`confirm` | `folder`、`folder_id` | 同 `update_twin_category_folder` |
| `delete_twin_category_folder` | 删除租户级孪生体类别文件夹 | `operational_data`、`folder_id`、`mode`、`confirm` | `delete_result`、`folder_id` | `POST /v1/{operationalData}/TwinCategory/assetFolder/{folderId}/delete`；禁止删除 TwinCategory 根文件夹 |
| `list_tenant_twin_categories` | 查询租户级已创建孪生体类别列表 | `operational_data`、`folder_id?`、`keyword?`、`page?`、`page_size?`、`include_all_folders?` | `items`、`total?`、`twinCategoryID`、`categoryType` | `POST /v1/{operationalData}/twinCategory/folder/{folderId}`；不传 `folder_id` 默认递归查询 TwinCategory 根及所有子文件夹；请求不传 `twinCategoryType` 过滤，避免漏掉 `DataObject` |
| `list_twin_categories` | 查询租户级已创建孪生体类别列表的兼容入口 | `operational_data`、`folder_id?`、`keyword?`、`page?`、`page_size?`、`include_all_folders?` | `items`、`total?`、`twinCategoryID`、`categoryType` | `POST /v1/{operationalData}/twinCategory/folder/{folderId}`；默认同 `list_tenant_twin_categories` |
| `get_twin_category` | 查询孪生体类别详情 | `operational_data`、`category_id` | `category`、`field_summary`、`model_config_summary` | `GET /v1/{operationalData}/twinCategory/{twinCategoryId}` |
| `create_twin_category` | 创建租户级孪生体类别 | `operational_data`、`folder_id`、`name`、`category_type`、`ledger_fields?`、`time_series_fields?`、`event_fields?`、`model_config?`、`mode`、`confirm` | `category`、`default_fields_used`、`save_payload` | 先校验 `category_type`；默认调用 `GET /v1/twinCategory/defaultField/{categoryType}`；再 `POST /v1/{operationalData}/twinCategory/Save` |
| `update_twin_category` | 修改孪生体类别定义 | `operational_data`、`category_id`、`patch`、`mode`、`confirm` | `category`、`diff?` | `POST /v1/{operationalData}/twinCategory/Save`；系统字段定义：`POST /v1/{operationalData}/twinCategory/{twinCategoryID}/updateSystemFieldDefinitions` |
| `rename_twin_category` | 重命名孪生体类别 | `operational_data`、`category_id`、`name`、`mode`、`confirm` | `category`、`save_payload` | 复用 `POST /v1/{operationalData}/twinCategory/Save`，只修改 `name` |
| `delete_twin_category` | 删除孪生体类别 | `operational_data`、`category_ids`、`mode`、`confirm` | `delete_result`、`successRows?`、`failRows?` | `POST /v1/{operationalData}/twinCategory/delete`；请求体为 `string[]` |
| `copy_twin_category` | 复制孪生体类别 | `category_ids`、`target_folder_id?`、`mode`、`confirm` | `copy_result`、`copied_category_ids?`、`move_result?` | `POST /v1/twinCategorys/copy`；请求体为 `string[]`；如传 `target_folder_id` 且后端返回新 ID，则再调用移动接口 |
| `move_twin_category` | 移动孪生体类别到指定文件夹 | `operational_data`、`category_ids`、`target_folder_id`、`mode`、`confirm` | `move_result`、`successRows?`、`failRows?` | `POST /v1/{operationalData}/twinCategory/move/folder/{folderId}`；请求体为 `string[]` |

`create_twin_category` 默认流程：

```text
list_twin_category_types
  -> get_twin_category_default_fields(category_type)
  -> list_twin_category_folders / create_twin_category_folder
  -> list_twin_categories 检查同名类别
  -> preview 返回 SaveTwinCategoryRequest
  -> execute + confirm=true 调用 Save
```

字段合并规则：如果用户没有显式传 `ledger_fields`、`time_series_fields`、`event_fields`，则使用 `get_twin_category_default_fields` 返回的默认字段；如果用户传入字段，则按字段 `field_id` 或 `field_name` 覆盖默认字段，新增字段追加到对应字段组。

`category` 建议统一为：

```json
{
  "category_id": "backend-id",
  "folder_id": "folder-id",
  "name": "孪生体类别",
  "category_type": "FixedEquipment",
  "description": "",
  "ledger_fields_count": 0,
  "timeseries_fields_count": 0,
  "event_fields_count": 0,
  "default_fields_used": true,
  "model_config": {},
  "raw": {}
}
```

### 7.7.1 场景级孪生体类别 Tools

| Tool | 中文说明 | MCP 入参 | MCP 出参 `data` | 后端接口映射 |
| --- | --- | --- | --- | --- |
| `list_scene_twin_category_folders` | 查询场景级孪生体类别文件夹 | `operational_data`、`scene_id`、`parent_folder_id?` | `root_folder`、`folders` | 不传 `parent_folder_id` 时先 `GET /v1/{operationalData}/location/{locationId}/TwinCategory/assetFolder/root`，再用根 `folderID` 调 `GET /v1/{operationalData}/location/{locationId}/TwinCategory/assetFolder/{parentFolderId}` |
| `create_scene_twin_category_folder` | 创建场景级孪生体类别文件夹 | `operational_data`、`scene_id`、`parent_folder_id?`、`name`、`mode`、`confirm` | `folder`、`parent_folder_id` | `POST /v1/{operationalData}/location/{locationId}/TwinCategory/assetFolder/{parentFolderId}/add` |
| `update_scene_twin_category_folder` | 编辑场景级孪生体类别文件夹名称 | `operational_data`、`scene_id`、`folder_id`、`name`、`mode`、`confirm` | `folder`、`folder_id` | `POST /v1/{operationalData}/location/{locationId}/TwinCategory/assetFolder/{folderId}/edit`；禁止编辑根文件夹 |
| `rename_scene_twin_category_folder` | 重命名场景级孪生体类别文件夹 | `operational_data`、`scene_id`、`folder_id`、`name`、`mode`、`confirm` | `folder`、`folder_id` | 同 `update_scene_twin_category_folder` |
| `delete_scene_twin_category_folder` | 删除场景级孪生体类别文件夹 | `operational_data`、`scene_id`、`folder_id`、`mode`、`confirm` | `delete_result`、`folder_id` | `POST /v1/{operationalData}/location/{locationId}/TwinCategory/assetFolder/{folderId}/delete`；禁止删除根文件夹 |
| `list_scene_twin_categories` | 查询场景文件夹下已启用类别配置 | `operational_data`、`scene_id`、`folder_id`、`level_id?`、`region_id?` | `categories`、`twinCategoryConfigID`、`twinCategoryID` | `POST /v1/{operationalData}/location/{locationId}/folder/{folderId}/twinCategory` |
| `add_scene_twin_category` | 将租户级类别加入场景并生成配置 | `operational_data`、`scene_id`、`folder_id`、`tenant_category_id`、`name`、`granularity?=1`、`granularity_type?=Year`、`mode`、`confirm` | `category`、`tenant_category_id` | `POST /v1/{operationalData}/location/{locationId}/folder/{folderId}/twinCategory/{twinCategoryId}`；默认按年粒度创建，避免后端默认秒级粒度 |
| `get_scene_twin_category` | 查询场景级类别配置详情 | `operational_data`、`scene_id`、`twin_category_config_id` | `category`、`field_summary`、`tenant_category_id` | `GET /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/details` |
| `update_scene_twin_category` | 编辑场景级类别配置名称 | `operational_data`、`scene_id`、`twin_category_config_id`、`name`、`mode`、`confirm` | `update_result` | `POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/editName` |
| `rename_scene_twin_category` | 重命名场景级类别配置 | `operational_data`、`scene_id`、`twin_category_config_id`、`name`、`mode`、`confirm` | `update_result` | 同 `update_scene_twin_category` |
| `delete_scene_twin_category` | 从场景中删除类别配置及其数据 | `operational_data`、`scene_id`、`twin_category_config_id`、`mode`、`confirm` | `delete_result` | `POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/delete`；不删除租户级类别模板 |

说明：场景级类别没有复制工具；后端虽有移动到文件夹接口，但本阶段按产品口径不暴露 `move_scene_twin_category`。如需调整所属文件夹，需后续单独确认是否开放。

### 7.8 孪生体类别字段 Tools

台账、时序、事件字段可以在 MCP 层保留三组 Tool，也可以在实现层复用一个内部方法。当前后端 JSON 未发现独立字段 CRUD 接口；字段新增、编辑、删除需通过“读取类别详情 -> 合并字段 -> `TwinCategorySave` 或 `updateSystemFieldDefinitions` 整包保存”的方式落地。场景内类别字段查询则使用 `LocationTwinCategoryData` 下的字段查询接口。

| Tool | 中文说明 | MCP 入参 | MCP 出参 `data` | 后端接口映射（待确认） |
| --- | --- | --- | --- | --- |
| `create_twin_ledger_field` | 创建孪生体台账字段 | `operational_data`、`category_id`、`field`、`mode`、`confirm` | `field`、`category` | 聚合实现：`GET /v1/{operationalData}/twinCategory/{twinCategoryId}` -> `POST /v1/{operationalData}/twinCategory/Save` |
| `list_twin_ledger_fields` | 查询场景孪生体台账字段 | `operational_data`、`scene_id`、`twin_category_config_id` | `fields` | 已实现：`GET /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/field` |
| `update_twin_ledger_field` | 修改孪生体台账字段 | `operational_data`、`category_id`、`field_id`、`patch`、`mode`、`confirm` | `field`、`diff?` | 聚合实现：读类别详情后整包 `POST /v1/{operationalData}/twinCategory/Save`；系统字段用 `/updateSystemFieldDefinitions` |
| `delete_twin_ledger_field` | 删除孪生体台账字段 | `operational_data`、`category_id`、`field_id`、`mode`、`confirm` | `delete_result` | 聚合实现：读类别详情后整包 `POST /v1/{operationalData}/twinCategory/Save` |
| `create_twin_timeseries_field` | 创建孪生体时序字段 | `operational_data`、`category_id`、`field`、`mode`、`confirm` | `field`、`category` | 聚合实现：`GET /v1/{operationalData}/twinCategory/{twinCategoryId}` -> `POST /v1/{operationalData}/twinCategory/Save` |
| `list_twin_timeseries_fields` | 查询孪生体时序字段 | `operational_data`、`category_id` 或 `scene_id+twin_category_config_id` | `items` | 租户级：`GET /v1/{operationalData}/twinCategory/{twinCategoryId}`；场景级：`GET /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/timeSeriesField` |
| `update_twin_timeseries_field` | 修改孪生体时序字段 | `operational_data`、`category_id`、`field_id`、`patch`、`mode`、`confirm` | `field`、`diff?` | 聚合实现：读类别详情后整包 `POST /v1/{operationalData}/twinCategory/Save` |
| `delete_twin_timeseries_field` | 删除孪生体时序字段 | `operational_data`、`category_id`、`field_id`、`mode`、`confirm` | `delete_result` | 聚合实现：读类别详情后整包 `POST /v1/{operationalData}/twinCategory/Save` |
| `create_twin_event_field` | 创建孪生体事件字段 | `operational_data`、`category_id`、`field`、`mode`、`confirm` | `field`、`category` | 聚合实现：`GET /v1/{operationalData}/twinCategory/{twinCategoryId}` -> `POST /v1/{operationalData}/twinCategory/Save` |
| `list_twin_event_fields` | 查询孪生体事件字段 | `operational_data`、`category_id` 或 `scene_id+twin_category_config_id` | `items` | 租户级：`GET /v1/{operationalData}/twinCategory/{twinCategoryId}`；场景级：`GET /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/eventField` |
| `update_twin_event_field` | 修改孪生体事件字段 | `operational_data`、`category_id`、`field_id`、`patch`、`mode`、`confirm` | `field`、`diff?` | 聚合实现：读类别详情后整包 `POST /v1/{operationalData}/twinCategory/Save` |
| `delete_twin_event_field` | 删除孪生体事件字段 | `operational_data`、`category_id`、`field_id`、`mode`、`confirm` | `delete_result` | 聚合实现：读类别详情后整包 `POST /v1/{operationalData}/twinCategory/Save` |

`field` 建议统一为：

```json
{
  "field_id": "backend-id",
  "category_id": "category-id",
  "field_group": "ledger|timeseries|event",
  "field_name": "code_name",
  "display_name": "显示名称",
  "data_type": "string|number|boolean|datetime|enum|geometry",
  "unit": "",
  "required": false,
  "default_value": null,
  "dictionary_ref": null,
  "raw": {}
}
```

### 7.9 孪生体类别模型配置 Tools

| Tool | 中文说明 | MCP 入参 | MCP 出参 `data` | 后端接口映射（待确认） |
| --- | --- | --- | --- | --- |
| `get_twin_category_model_config` | 查询孪生体类别模型配置 | `operational_data`、`category_id`，或 `scene_id+twin_category_config_id` | `model_config` | 租户级：`GET /v1/{operationalData}/twinCategory/{twinCategoryId}/model`；场景级：`GET /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/model` |
| `update_twin_category_model_config` | 修改孪生体类别模型配置 | `operational_data`、`category_id` 或 `scene_id+twin_category_config_id`、`patch`、`mode`、`confirm` | `model_config`、`diff?` | 租户级：读后整包 `POST /v1/{operationalData}/twinCategory/Save`；场景级：`POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/model/edit` |
| `get_twin_category_base_setting` | 查询孪生体类别基础设置 | `operational_data`、`category_id` 或 `scene_id+twin_category_config_id` | `base_setting` | 租户级：`GET /v1/{operationalData}/twinCategory/{twinCategoryId}/setting`；场景级：`GET /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/setting` |
| `update_twin_category_base_setting` | 修改孪生体类别基础设置 | `operational_data`、`category_id` 或 `scene_id+twin_category_config_id`、`patch`、`mode`、`confirm` | `base_setting` | 租户级：读后整包 `POST /v1/{operationalData}/twinCategory/Save`；场景级：`POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/setting` |
| `get_twin_category_selection_setting` | 查询对象选中设置 | 同上 | `selection_setting` | 从 `setting` 返回体中抽取 |
| `update_twin_category_selection_setting` | 修改对象选中设置 | 同上 + `patch` | `selection_setting` | 合并到 `setting` 后保存 |
| `get_twin_category_label_setting` | 查询标签设置 | 同上 | `label_setting` | 从 `setting` 返回体中抽取 |
| `update_twin_category_label_setting` | 修改标签设置 | 同上 + `patch` | `label_setting` | 合并到 `setting` 后保存 |
| `get_twin_category_visibility_setting` | 查询可见性设置 | 同上 | `visibility_setting` | 从 `setting` 返回体中抽取 |
| `update_twin_category_visibility_setting` | 修改可见性设置 | 同上 + `patch` | `visibility_setting` | 合并到 `setting` 后保存 |
| `create_twin_joint_binding` | 创建模型关节绑定 | `category_id` 或 `twin_category_config_id`、`binding`、`mode`、`confirm` | `binding` | 后端未发现独立关节接口；合并到 `model_config` 后保存 |
| `update_twin_joint_binding` | 修改模型关节绑定 | `binding_id`、`patch`、`mode`、`confirm` | `binding` | 后端未发现独立关节接口；合并到 `model_config` 后保存 |
| `delete_twin_joint_binding` | 删除模型关节绑定 | `binding_id`、`mode`、`confirm` | `delete_result` | 后端未发现独立关节接口；合并到 `model_config` 后保存 |
| `create_twin_animation_binding` | 创建模型动画绑定 | `category_id` 或 `twin_category_config_id`、`binding`、`mode`、`confirm` | `binding` | 后端未发现独立动画接口；合并到 `model_config` 后保存 |
| `update_twin_animation_binding` | 修改模型动画绑定 | `binding_id`、`patch`、`mode`、`confirm` | `binding` | 后端未发现独立动画接口；合并到 `model_config` 后保存 |
| `delete_twin_animation_binding` | 删除模型动画绑定 | `binding_id`、`mode`、`confirm` | `delete_result` | 后端未发现独立动画接口；合并到 `model_config` 后保存 |
| `create_twin_sensor_binding` | 创建传感器绑定 | `category_id` 或 `twin_category_config_id`、`binding`、`mode`、`confirm` | `binding` | 后端未发现独立传感器接口；合并到 `model_config` 后保存 |
| `update_twin_sensor_binding` | 修改传感器绑定 | `binding_id`、`patch`、`mode`、`confirm` | `binding` | 后端未发现独立传感器接口；合并到 `model_config` 后保存 |
| `delete_twin_sensor_binding` | 删除传感器绑定 | `binding_id`、`mode`、`confirm` | `delete_result` | 后端未发现独立传感器接口；合并到 `model_config` 后保存 |

模型配置类工具默认使用字段级合并。若后端只能整包保存，MCP 必须先读后写，避免丢失未知配置。

### 7.10 场景孪生体实例 Tools

| Tool | 中文说明 | MCP 入参 | MCP 出参 `data` | 后端接口映射（待确认） |
| --- | --- | --- | --- | --- |
| `create_twin_instance` | 创建场景中的孪生体实例/台账数据 | `operational_data`、`scene_id`、`twin_category_config_id`、`ledger_data_json`、`granularity_type?=Year`、`mode`、`confirm` | `instance` | 已实现：`POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/data/add`；请求体 `content` 为 JSON 字符串；preview/execute 预校验时间格式 |
| `list_twin_instances` | 查询场景孪生体实例列表 | `operational_data`、`scene_id`、`twin_category_config_id`、`level_id?`、`region_id?`、`condition_json?`、`page?`、`page_size?` | `rows`、`total` | 已实现：`POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/data` |
| `get_twin_instance` | 查询单个孪生体实例详情 | `operational_data`、`scene_id`、`twin_category_config_id`、`instance_id` | `instance` | 已实现：调用列表接口后按 `twinCategoryDataID` 或 `instanceNumber` 过滤 |
| `update_twin_instance` | 修改孪生体实例台账数据 | `operational_data`、`scene_id`、`twin_category_config_id`、`instance_id`、`ledger_data_json`、`granularity_type?=Year`、`mode`、`confirm` | `update_result` | 已实现：`POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/data/batchEdit`；请求体为 `EditLocationTwinCategoryDataRequest[]`；preview/execute 预校验时间格式 |
| `delete_twin_instance` | 删除孪生体实例 | `operational_data`、`scene_id`、`twin_category_config_id`、`instance_ids`、`mode`、`confirm` | `delete_result` | 已实现：`POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/data/delete`；请求体为 `string[]` |
| `copy_twin_instance` | 复制孪生体实例 | `source_instance_id`、`target_name`、`target_scene_id?`、`mode`、`confirm` | `source_instance_id`、`target_instance` | 后端未发现独立实例复制接口；MCP 读取源实例后调用 `/data/add` 创建副本 |
| `move_twin_instance` | 移动实例到层级/区域或更新空间位置 | `operational_data`、`scene_id`、`twin_category_config_id`、`instance_id`、`target_hierarchy_id?`、`target_region_id?`、`position?`、`mode`、`confirm` | `instance` | 通过 `/data/batchEdit` 更新 `belongToLevelID`、`belongToRegionID` 或位置内容 |
| `bind_twin_instance_resource` | 给实例绑定模型、视频或跳转资源 | `operational_data`、`scene_id`、`twin_category_config_id`、`instance_id`、`resource_ref`、`binding_role?`、`mode`、`confirm` | `instance`、`resource_refs` | 模型资源通常绑定在类别模型配置；视频/跳转：`POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/updateVideoAndJumpLocation` 或 `/updateVideo` |
| `unbind_twin_instance_resource` | 解绑实例资源 | 同上 | `instance`、`resource_refs` | 通过对应绑定接口写空或更新实例内容；具体 DTO 待校准 |
| `preview_twin_instance` | 预览或返回实例查看上下文 | `operational_data`、`scene_id`、`twin_category_config_id`、`instance_id` | `preview_url?`、`preview_payload?` | 后端未发现独立实例预览接口；MCP 返回场景访问上下文和实例详情 |

`instance` 建议统一为：

```json
{
  "instance_id": "backend-id",
  "scene_id": "scene-id",
  "category_id": "category-id",
  "hierarchy_id": "hierarchy-id",
  "name": "实例名称",
  "position": { "x": 0, "y": 0, "z": 0, "coordinate_system": "EPSG:4326" },
  "rotation": { "x": 0, "y": 0, "z": 0 },
  "scale": { "x": 1, "y": 1, "z": 1 },
  "resource_refs": [],
  "ledger_data": {},
  "raw": {}
}
```

实例台账写入约束：

- ledger_data_json 是后端 /data 接口的 content 字符串，必须是 JSON 对象，字段名应来自 list_twin_ledger_fields 返回的后端字段名。
- 写入实例时 MCP 会按 `granularity_type + granularity` 归一化实例时间：Year 表示当前年 + `granularity` 月 + 1 日；Month 表示当前年月 + `granularity` 日；Day 表示当前年月日 + `granularity` 小时，最终格式统一为 `yyyy-MM-dd HH:mm:ss`。
- 位置/经纬度字段不由 MCP 自动生成；如果实例需要落位，必须在 ledger_data_json 中显式传入已经确认来源的字段和值。

实例数据工具：

| Tool | 中文说明 | MCP 入参 | MCP 出参 `data` | 后端接口映射（待确认） |
| --- | --- | --- | --- | --- |
| `upsert_twin_instance_ledger_data` | 新增或更新实例台账数据 | `operational_data`、`scene_id`、`twin_category_config_id`、`ledger_data_json`、`instance_id?`、`granularity_type?=Year`、`mode`、`confirm` | `instance` 或 `update_result` | 已实现：无 `instance_id` 走 `/data/add`，有 `instance_id` 走 `/data/batchEdit`；按 Year/Month/Day 粒度归一化实例时间字段 |
| `list_twin_instance_ledger_data` | 查询实例台账数据 | `operational_data`、`scene_id`、`twin_category_config_id`、`level_id?`、`region_id?`、`condition_json?`、`page?`、`page_size?` | `rows`、`total` | 已实现：`POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/data` |
| `delete_twin_instance_ledger_data` | 删除实例台账数据 | `operational_data`、`scene_id`、`twin_category_config_id`、`instance_ids`、`mode`、`confirm` | `delete_result` | 已实现：`POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/data/delete` |
| `upsert_twin_instance_timeseries_data` | 写入实例时序数据 | `operational_data`、`scene_id`、`twin_category_config_id`、`rows`、`mode`、`confirm` | `upsert_result` | `POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/addTimeSeriesData` |
| `list_twin_instance_timeseries_data` | 查询实例时序数据 | `operational_data`、`scene_id`、`twin_category_config_id`、`field_names?`、`time_range?`、`page?`、`page_size?` | `items`、`total` | `GET/POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/typeDistinguish/{typeDistinguish}`，其中 `typeDistinguish=TwinTimeSeries` |
| `delete_twin_instance_timeseries_data` | 删除实例时序数据 | `operational_data`、`scene_id`、`twin_category_config_id`、`time_range?`、`row_ids?`、`mode`、`confirm` | `delete_result` | `POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/deleteTimeSeriesData` |
| `upsert_twin_instance_event_data` | 写入实例事件数据 | `operational_data`、`scene_id`、`twin_category_config_id`、`rows`、`mode`、`confirm` | `upsert_result` | `POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/addEventData` |
| `list_twin_instance_event_data` | 查询实例事件数据 | `operational_data`、`scene_id`、`twin_category_config_id`、`event_type?`、`time_range?`、`page?`、`page_size?` | `items`、`total` | `GET/POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/typeDistinguish/{typeDistinguish}`，其中 `typeDistinguish=TwinEvent` |
| `delete_twin_instance_event_data` | 删除实例事件数据 | `operational_data`、`scene_id`、`twin_category_config_id`、`time_range?`、`row_ids?`、`mode`、`confirm` | `delete_result` | `POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/deleteEventData` |

### 7.11 资源管理 Tools

资源管理是租户级可变更资源。P0 先支持文件夹、模型、图片、视频、媒体、数据表资源的上传、查询、编辑、删除、预览、绑定、解绑。

| Tool | 中文说明 | MCP 入参 | MCP 出参 `data` | 后端接口映射（待确认） |
| --- | --- | --- | --- | --- |
| `create_resource_folder` | 创建资源文件夹 | `operational_data`、`parent_id`、`name`、`resource_type`、`mode`、`confirm` | `folder` | `POST /v1/{operationalData}/{assetFolderType}/assetFolder/{parentFolderId}/add` |
| `list_resource_folders` | 查询资源文件夹 | `operational_data`、`parent_id?`、`resource_type`、`tree?` | `items` 或 `tree` | 根：`GET /v1/{operationalData}/{assetFolderType}/assetFolder/root`；子级：`GET /v1/{operationalData}/{assetFolderType}/assetFolder/{parentFolderId}` |
| `update_resource_folder` | 修改资源文件夹 | `operational_data`、`folder_id`、`resource_type`、`patch`、`mode`、`confirm` | `folder` | `POST /v1/{operationalData}/{assetFolderType}/assetFolder/{folderId}/edit` |
| `delete_resource_folder` | 删除资源文件夹 | `operational_data`、`folder_id`、`resource_type`、`mode`、`confirm` | `delete_result`、`dependency_summary` | `POST /v1/{operationalData}/{assetFolderType}/assetFolder/{folderId}/delete` |
| `begin_resource_upload` | 开始上传资源文件 | `file_name`、`file_size`、`content_type?`、`resource_type`、`folder_id?`、`metadata?` | `upload_id`、`upload_url?`、`upload_headers?` | 标准 begin/commit 未发现；模型 TGM 导入用 `POST /v1/asset/folder/{folderID}/tgm/import`；媒体导入用 `POST /v1/{operationalData}/assetLibrary/asset/importMediaFiles`；数据表上传用 `POST /v1/{operationalData}/dataSourceFolders/{dataSourceFolderID}/dataSource/upload` |
| `commit_resource_upload` | 完成资源上传并生成资源记录 | `upload_id`、`metadata?`、`mode`、`confirm` | `resource_ref`、`resource` | 与具体上传接口合并执行；未发现独立 commit 接口 |
| `cancel_resource_upload` | 取消资源上传 | `upload_id` | `cancelled` | 未发现独立取消上传接口 |
| `list_resources` | 查询资源列表 | `operational_data`、`resource_type?`、`folder_id?`、`keyword?`、`page?`、`page_size?` | `items`、`total` | 文件夹内：`POST /v1/{operationalData}/folder/{folderID}/assets`；全局检索：`POST /v1/{operationalData}/assets` |
| `get_resource` | 查询资源详情 | `operational_data?`、`resource_ref` | `resource` | `GET /v1/{operationalData}/asset/{assetID}/detail` 或 `GET /v1/asset/{assetID}/detail`；模型编辑详情：`GET /v1/asset/{assetID}` |
| `update_resource` | 修改资源元数据 | `resource_ref`、`patch`、`mode`、`confirm` | `resource`、`diff?` | 模型：`POST /v1/asset/{assetID}/editModel`；其他资源类型更新接口需按前端调用继续确认 |
| `delete_resource` | 删除资源 | `resource_ref` 或 `asset_ids`、`mode`、`confirm` | `delete_result`、`dependency_summary` | `POST /v1/assets/delete` |
| `preview_resource` | 预览或获取资源文件访问地址 | `resource_ref`、`asset_usage?`、`expand_name?`、`file_name?` | `preview_url?`、`preview_payload?` | `GET /v1/assetLibrary/{assetUsage}/asset/{assetID}/{expandName}/{fileName}` |
| `bind_resource` | 将资源绑定到类别、实例或配置 | `resource_ref`、`target_type`、`target_id`、`binding_role?`、`mode`、`confirm` | `binding` | 类别/实例绑定多落在类别模型配置、实例内容或视频绑定接口中；无统一资源绑定接口 |
| `unbind_resource` | 从类别、实例或配置解绑资源 | `resource_ref`、`target_type`、`target_id`、`binding_role?`、`mode`、`confirm` | `unbind_result` | 无统一资源解绑接口；按目标对象配置写空或移除引用 |

`resource` 建议统一为：

```json
{
  "resource_ref": {
    "resource_id": "backend-id",
    "resource_code": "optional-code",
    "asset_id": "optional-asset-id",
    "resource_type": "model|image|video|media|data_table",
    "raw": {}
  },
  "name": "资源名称",
  "folder_id": "folder-id",
  "file_name": "source.ext",
  "file_size": 0,
  "content_type": "",
  "preview_url": "",
  "metadata": {},
  "create_time": "",
  "update_time": ""
}
```

### 7.12 P0 后端接口校准清单

基于 `backend-config-api.json` 和 `swagger.json` 的第一轮校准结果：

| 校准项 | 状态 | 说明 |
| --- | --- | --- |
| 认证 | 已定位主接口 | `POST /v1/login`、`GET /v1/my/info`、`GET/POST /v1/token`；独立 refresh 接口未发现 |
| 场景 | 已定位主接口 | Location 对应场景；CRUD、复制、导入、导出已定位；导出下载 ZIP 需要补二进制/文件流能力，导入需要补 multipart 文件来源约定 |
| 层级 | 已定位 | `level` 系列接口覆盖查询、新增、编辑、删除、上移下移 |
| 点线面 | 已定位 | `position`=POI、`path`=指引路线、`region`=重点区域 |
| 孪生体类别 | 已定位 | 租户级类别使用 `twinCategory` + `assetFolderType=TwinCategory`；类别类型来自 `TwinCategoryTypes` 枚举，默认字段使用 `GET /v1/twinCategory/defaultField/{categoryType}` |
| 类别字段 | 部分已定位 | 字段查询已定位；独立字段 CRUD 未发现，需通过类别整包保存或系统字段定义接口落地 |
| 模型配置 | 部分已定位 | 租户级模型/设置查询已定位；场景级模型/设置读写已定位；关节/动画/传感器独立接口未发现 |
| 孪生体实例 | 已定位主接口 | 实例即场景孪生体类别数据；新增、列表、批量编辑、删除、时序/事件增删已定位 |
| 资源管理 | 部分已定位 | 资源文件夹、资产查询、详情、模型编辑、删除、预览已定位；统一 begin/commit 上传接口未发现 |
| 删除预检 | 待确认 | 未发现通用依赖预检接口；MCP 需要组合查询并在 `preview` 中返回影响范围 |

场景导入/导出补充说明：

1. `export_scene` 建议先实现“生成 ZIP + 返回下载接口信息”的最小版本；真正把 ZIP 文件保存到本地需要 `TwinBackendClient` 增加二进制/文件流下载能力。
2. `import_scene` 是 `multipart/form-data` 上传接口，当前 MCP 需要先确定文件来源约定：本地路径、MCP 资源引用、或前置上传得到的 `file_upload_id`。
3. `POST /v1/location/{locationId}/import` 属于“更新已有地点包”，风险高于新增导入，默认必须 `preview` 并要求确认目标场景名称。

## 8. P1 Tools 设计

### 8.1 数据对象

数据对象类别与孪生体类别是并行体系。数据对象表达数据信息，主要用于图表、图层显示；孪生体实例表达场景中可显示的对象，也可参与统计和可视化。

| Tool | 能力 |
| --- | --- |
| `create_data_object_category` | 创建数据对象类别 |
| `list_data_object_categories` | 查询数据对象类别 |
| `get_data_object_category` | 查询数据对象类别详情 |
| `update_data_object_category` | 编辑数据对象类别 |
| `delete_data_object_category` | 删除数据对象类别 |
| `create_data_object_field` | 创建字段定义 |
| `list_data_object_fields` | 查询字段定义 |
| `update_data_object_field` | 编辑字段定义 |
| `delete_data_object_field` | 删除字段定义 |
| `import_data_object_data` | 导入数据对象数据 |
| `upsert_data_object_data` | 新增或更新数据对象数据 |
| `list_data_object_data` | 查询数据对象数据 |
| `delete_data_object_data` | 删除数据对象数据 |

### 8.2 对象透视表

对象透视表与数据对象类似，区别是来源不是直接导入数据，而是来自孪生体数据或数据对象数据的组合整理结果。其结果可供图表、图层配置显示。

| Tool | 能力 |
| --- | --- |
| `create_object_pivot` | 创建对象透视表 |
| `list_object_pivots` | 查询对象透视表 |
| `get_object_pivot` | 查询透视表详情 |
| `update_object_pivot` | 编辑透视表配置 |
| `delete_object_pivot` | 删除透视表 |
| `preview_object_pivot_result` | 预览透视结果 |
| `refresh_object_pivot_result` | 刷新透视结果 |

透视表核心参数：

| 参数 | 说明 |
| --- | --- |
| `source_refs` | 来源，可包含孪生体台账/时序/事件数据和数据对象数据 |
| `dimensions` | 维度字段，如时间、空间、类别 |
| `measures` | 指标字段和聚合方式 |
| `filters` | 过滤条件 |
| `output_schema` | 输出字段结构 |

### 8.3 图层、图表、业务主题

| Tool | 能力 |
| --- | --- |
| `create_layer_folder` / `create_layer` | 创建图层文件夹和图层 |
| `list_layers` / `get_layer` | 查询图层 |
| `update_layer` / `delete_layer` | 编辑或删除图层 |
| `create_chart_folder` / `create_chart` | 创建图表文件夹和图表 |
| `list_charts` / `get_chart` | 查询图表 |
| `update_chart` / `delete_chart` | 编辑或删除图表 |
| `create_business_theme` | 创建业务主题 |
| `list_business_themes` / `get_business_theme` | 查询业务主题 |
| `update_business_theme` / `delete_business_theme` | 编辑或删除业务主题 |
| `bind_theme_object` / `unbind_theme_object` | 绑定或解绑场景类型对象 |
| `bind_theme_layer` / `unbind_theme_layer` | 绑定或解绑图层 |
| `bind_theme_chart` / `unbind_theme_chart` | 绑定或解绑图表 |

### 8.4 告警和视频

视频服务改造、IVS 剥离、自研视频特征识别先不作为当前设计重点，但保留 MCP 对象入口。

| Tool | 能力 |
| --- | --- |
| `create_alarm_folder` / `create_alarm_rule` | 创建告警文件夹和告警规则 |
| `list_alarm_rules` / `get_alarm_rule` | 查询告警规则 |
| `update_alarm_rule` / `delete_alarm_rule` | 编辑或删除告警规则 |
| `create_alarm_level` / `update_alarm_level` | 配置告警级别 |
| `list_alarm_data` / `get_alarm_data` | 查询告警数据 |
| `create_video_service` | 创建视频服务 |
| `list_video_services` / `get_video_service` | 查询视频服务 |
| `update_video_service` / `delete_video_service` | 编辑或删除视频服务 |
| `create_video_resource` | 创建视频资源 |
| `bind_video_alarm_data` | 绑定视频告警数据 |

## 9. P2 Tools 设计

P2 覆盖剩余后台配置和高级能力。P2 仍按对象粒度拆分 MCP Tool，但默认优先级低于 P0/P1；其中用户、密码、数据集成连接信息等敏感对象必须加强确认、脱敏和权限控制。

### 9.1 过滤器

过滤器用于对场景、图层、图表或业务主题中的数据展示做条件筛选。

| Tool | 能力 |
| --- | --- |
| `create_filter_folder` | 创建过滤文件夹 |
| `list_filter_folders` | 查询过滤文件夹 |
| `update_filter_folder` | 编辑过滤文件夹 |
| `delete_filter_folder` | 删除过滤文件夹 |
| `create_filter` | 创建过滤器 |
| `list_filters` | 查询过滤器 |
| `get_filter` | 查询过滤器详情 |
| `update_filter` | 编辑过滤器 |
| `delete_filter` | 删除过滤器 |
| `create_filter_item` | 创建过滤项 |
| `list_filter_items` | 查询过滤项 |
| `update_filter_item` | 编辑过滤项 |
| `delete_filter_item` | 删除过滤项 |

### 9.2 首页配置

| Tool | 能力 |
| --- | --- |
| `get_homepage_config` | 查询首页配置 |
| `update_homepage_config` | 修改首页配置 |
| `preview_homepage_config` | 预览首页配置 |
| `reset_homepage_config` | 重置首页配置 |

首页配置类工具默认使用 `preview` + `execute`，避免覆盖用户已有门户配置。

### 9.3 睿司指令控制

| Tool | 能力 |
| --- | --- |
| `get_ruisi_panel_config` | 查询睿司面板配置 |
| `update_ruisi_panel_config` | 修改睿司面板配置 |
| `test_ruisi_panel_config` | 测试睿司面板配置 |
| `enable_ruisi_panel_config` | 启用睿司面板配置 |
| `disable_ruisi_panel_config` | 停用睿司面板配置 |

睿司控制类工具如果会触发真实设备、外部系统或指令下发，必须先返回风险提示，并要求 `confirm=true`。

### 9.4 数据集成：结构化数据库

| Tool | 能力 |
| --- | --- |
| `create_database_service` | 创建数据库服务 |
| `list_database_services` | 查询数据库服务 |
| `get_database_service` | 查询数据库服务详情 |
| `update_database_service` | 编辑数据库服务 |
| `delete_database_service` | 删除数据库服务 |
| `test_database_service` | 测试数据库连接 |
| `create_database_table_folder` | 创建数据表文件夹 |
| `list_database_table_folders` | 查询数据表文件夹 |
| `create_database_table_link` | 创建数据表链接 |
| `list_database_table_links` | 查询数据表链接 |
| `get_database_table_link` | 查询数据表链接详情 |
| `update_database_table_link` | 编辑数据表链接 |
| `delete_database_table_link` | 删除数据表链接 |
| `preview_database_table_data` | 预览数据表数据 |

数据库密码、连接串、密钥等敏感字段只允许写入和脱敏摘要返回，不允许原文回显。

### 9.5 数据集成：数据接口

| Tool | 能力 |
| --- | --- |
| `create_api_service` | 创建接口服务 |
| `list_api_services` | 查询接口服务 |
| `get_api_service` | 查询接口服务详情 |
| `update_api_service` | 编辑接口服务 |
| `delete_api_service` | 删除接口服务 |
| `test_api_service` | 测试接口服务 |
| `create_receive_api_folder` | 创建数据接收接口文件夹 |
| `list_receive_api_folders` | 查询数据接收接口文件夹 |
| `create_receive_api` | 创建数据接收接口 |
| `list_receive_apis` | 查询数据接收接口 |
| `get_receive_api` | 查询数据接收接口详情 |
| `update_receive_api` | 编辑数据接收接口 |
| `delete_receive_api` | 删除数据接收接口 |
| `test_receive_api` | 测试数据接收接口 |

### 9.6 数据集成：网关

| Tool | 能力 |
| --- | --- |
| `create_gateway_server` | 创建网关服务器 |
| `list_gateway_servers` | 查询网关服务器 |
| `get_gateway_server` | 查询网关服务器详情 |
| `update_gateway_server` | 编辑网关服务器 |
| `delete_gateway_server` | 删除网关服务器 |
| `test_gateway_server` | 测试网关服务器 |
| `create_gateway_subscribe_folder` | 创建网关订阅接口文件夹 |
| `create_gateway_subscribe_api` | 创建网关订阅接口 |
| `list_gateway_subscribe_apis` | 查询网关订阅接口 |
| `update_gateway_subscribe_api` | 编辑网关订阅接口 |
| `delete_gateway_subscribe_api` | 删除网关订阅接口 |
| `create_gateway_publish_folder` | 创建网关发布接口文件夹 |
| `create_gateway_publish_api` | 创建网关发布接口 |
| `list_gateway_publish_apis` | 查询网关发布接口 |
| `update_gateway_publish_api` | 编辑网关发布接口 |
| `delete_gateway_publish_api` | 删除网关发布接口 |

### 9.7 数据字典

| Tool | 能力 |
| --- | --- |
| `create_dictionary_folder` | 创建字典文件夹 |
| `list_dictionary_folders` | 查询字典文件夹 |
| `update_dictionary_folder` | 编辑字典文件夹 |
| `delete_dictionary_folder` | 删除字典文件夹 |
| `create_dictionary_data` | 创建字典数据 |
| `list_dictionary_data` | 查询字典数据 |
| `get_dictionary_data` | 查询字典数据详情 |
| `update_dictionary_data` | 编辑字典数据 |
| `delete_dictionary_data` | 删除字典数据 |
| `import_dictionary_data` | 导入字典数据 |
| `export_dictionary_data` | 导出字典数据 |

### 9.8 同步管理

| Tool | 能力 |
| --- | --- |
| `list_standard_twin_sync_items` | 查询标准孪生体同步项 |
| `get_standard_twin_sync_item` | 查询标准孪生体同步详情 |
| `run_standard_twin_sync` | 执行标准孪生体同步 |
| `list_data_object_sync_items` | 查询数据对象同步项 |
| `get_data_object_sync_item` | 查询数据对象同步详情 |
| `run_data_object_sync` | 执行数据对象同步 |
| `get_sync_task_status` | 查询同步任务状态 |
| `get_sync_task_log` | 查询同步任务日志 |

同步类工具是长任务，提交后返回 `accepted`，后续通过 `get_sync_task_status` 轮询。

### 9.9 用户与密码管理

| Tool | 能力 |
| --- | --- |
| `list_users` | 查询用户列表 |
| `get_user` | 查询用户详情 |
| `update_user` | 编辑用户信息 |
| `enable_user` | 启用用户 |
| `disable_user` | 停用用户 |
| `reset_user_password` | 重置用户密码 |
| `change_current_user_password` | 修改当前用户密码 |

用户和密码管理属于高风险对象。P2 默认建议只开放查询和低风险编辑；重置密码、停用用户必须要求 `confirm=true`，并记录操作人。

### 9.10 场景显示与导览

| Tool | 能力 |
| --- | --- |
| `get_scene_display_config` | 查询场景显示配置 |
| `update_scene_display_config` | 修改场景显示配置 |
| `create_scene_tour` | 创建导览 |
| `list_scene_tours` | 查询导览 |
| `get_scene_tour` | 查询导览详情 |
| `update_scene_tour` | 编辑导览 |
| `delete_scene_tour` | 删除导览 |
| `create_scene_tour_step` | 创建导览步骤 |
| `list_scene_tour_steps` | 查询导览步骤 |
| `update_scene_tour_step` | 编辑导览步骤 |
| `delete_scene_tour_step` | 删除导览步骤 |
| `move_scene_tour_step` | 调整导览步骤顺序 |
| `get_scene_tool_config` | 查询场景工具配置 |
| `update_scene_tool_config` | 修改场景工具配置 |

### 9.11 固定资产和公共资产读取

资源资产、公共/社区资产、粒子资源、材质资源当前按固定资产处理，P2 先以只读和预览为主。

| Tool | 能力 |
| --- | --- |
| `list_asset_library_folders` | 查询资产库文件夹 |
| `list_model_assets` | 查询模型资产 |
| `list_image_assets` | 查询图片资产 |
| `list_video_assets` | 查询视频资产 |
| `list_video_service_assets` | 查询视频服务资产 |
| `list_community_assets` | 查询社区资产 |
| `list_particle_assets` | 查询粒子资源 |
| `list_material_assets` | 查询材质资源 |
| `get_asset_detail` | 查询资产详情 |
| `preview_asset` | 预览资产 |

若后续确认固定资产允许租户级维护，再补充 `create/update/delete` 类工具。

### 9.12 P2 后端接口校准清单

| 校准项 | 需要确认的内容 |
| --- | --- |
| 过滤器 | 过滤器与过滤项的数据结构、绑定对象、执行范围 |
| 首页配置 | 首页配置是否整包保存，是否有默认模板和重置接口 |
| 睿司 | 面板配置是否会触发真实指令，测试接口是否安全 |
| 数据库 | 连接串、密码、表链接、预览数据的接口和脱敏策略 |
| 数据接口 | 接口服务、接收接口的鉴权配置和测试返回结构 |
| 网关 | 订阅/发布接口、MQTT 或其他协议字段 |
| 数据字典 | 字典文件夹、字典数据的层级和导入导出格式 |
| 同步管理 | 同步任务提交、状态、日志接口 |
| 用户 | 用户管理权限、密码策略、操作审计 |
| 导览 | 导览步骤顺序、相机位、动作配置结构 |
| 固定资产 | 固定资产是否只读，是否可被租户复制或引用 |

## 10. 四版本实施路线

接口实现建议分 4 个版本推进：先用约 10 个接口跑通最小闭环，再按 P0 完整、P1 应用、P2 扩展三个节点版本展开。版本切分以“可演示闭环”为优先，不按对象数量平均分配。

### 10.1 V1.0：本周最小闭环版本

V1.0 目标是在本周实现约 10 个 MCP Tool，把“AI 创建基础场景、创建层级并放入一个兴趣点位 POI”的链路跑通。这个版本先不做复杂上传、孪生体类别/实例、字段粒子化、图层图表、告警视频和高级删除预检。

| 优先级 | MCP Tool | 中文说明 | 为什么本周先做 |
| --- | --- | --- | --- |
| 1 | `login_twin_backend` | 登录孪易后端并缓存 Token | 所有后端接口调用的前置条件 |
| 2 | `get_twin_mcp_context` | 获取 MCP 和当前后端上下文 | 便于 Agent 判断连接、用户、操作域 |
| 3 | `list_scenes` | 查询场景列表 | 避免重复创建，支持定位已有场景 |
| 4 | `create_scene` | 创建场景/地点 | 最小闭环的根对象 |
| 5 | `get_scene` | 查询场景详情 | 创建后回读验证 |
| 6 | `create_scene_hierarchy` | 创建场景层级 | POI、路线、区域和后续实例都需要层级承载 |
| 7 | `list_scene_hierarchies` | 查询场景层级 | 创建后回读，并给 POI 定位层级 |
| 8 | `create_scene_poi` | 创建兴趣点位 POI | POI 是最轻量的场景对象，接口清晰，适合第一周跑通写入 |
| 9 | `list_scene_pois` | 查询兴趣点位列表 | 验证 POI 创建结果，支撑演示 |
| 10 | `get_scene_poi` | 查询单个兴趣点位详情 | 验证单对象详情回读和 MCP 过滤逻辑 |

V1.0 可选第 11 个接口：

| MCP Tool | 中文说明 | 触发条件 |
| --- | --- | --- |
| `update_scene_poi` | 修改兴趣点位 POI | 如果本周希望证明写入更新能力，不只做新增 |
| `delete_scene_poi` | 删除兴趣点位 POI | 如果本周希望证明删除确认链路 |


V1.0 演示脚本：

```text
login_twin_backend
get_twin_mcp_context
list_scenes
create_scene
get_scene
create_scene_hierarchy
list_scene_hierarchies
create_scene_poi
list_scene_pois
get_scene_poi
```

V1.0 验收口径：

| 编号 | 验收项 |
| --- | --- |
| V1.0-01 | MCP Server 可登录孪易后端，并自动携带 Token 调用接口 |
| V1.0-02 | 可创建一个新场景并回读到 `locationId`、名称和基础配置 |
| V1.0-03 | 可在场景下创建一个层级并查询回来 |
| V1.0-04 | 可在指定层级下创建一个 POI 点位 |
| V1.0-05 | 可查询 POI 列表并回读单个 POI 详情 |
| V1.0-06 | 所有 Tool 返回统一 `status/summary/data/warnings/affected_objects/next_actions` |

### 10.2 V1.1：P0 完整对象版本

V1.1 目标是把 P0 对象补齐，让基础场景配置可以覆盖完整后台主流程。

| 模块 | 需要补齐的 MCP Tool |
| --- | --- |
| 场景管理 | `update_scene`、`delete_scene`、`copy_scene`、`export_scene`、`import_scene` |
| 场景层级 | `get_scene_hierarchy`、`update_scene_hierarchy`、`delete_scene_hierarchy`、`move_scene_hierarchy`、`rename_scene_hierarchy` |
| 点线面 | POI、指引路线、重点区域的 `create/list/get/update/delete` |
| 孪生体类别 | `list_twin_category_types`、`get_twin_category_default_fields`、`list_twin_category_folders`、`create_twin_category_folder`、`list_tenant_twin_categories`、`list_twin_categories`、`get_twin_category`、`create_twin_category`、`update_twin_category`、`rename_twin_category`、`delete_twin_category`、`copy_twin_category`、`move_twin_category` |
| 实例管理 | `create_twin_instance`、`list_twin_instances`、`get_twin_instance`、`update_twin_instance`、`delete_twin_instance`、`move_twin_instance`、`copy_twin_instance` |
| 实例数据 | 台账、时序、事件数据的 `upsert/list/delete` |

V1.1 验收口径：可完整创建、修改、删除基础场景结构、点线面和孪生体实例，并支持删除前 `preview` 风险提示。

### 10.3 V1.2：P1 数据可视化应用版本

V1.2 目标是让 Agent 能在已有场景上搭建数据和可视化应用。

| 模块 | 需要补齐的 MCP Tool |
| --- | --- |
| 数据对象 | 数据对象类别、字段、数据导入、数据查询 |
| 对象透视表 | 透视表创建、查询、更新、预览、刷新 |
| 图层 | 图层文件夹、图层 CRUD、数据绑定 |
| 图表 | 图表文件夹、图表 CRUD、数据绑定 |
| 业务主题 | 业务主题 CRUD、绑定对象/图层/图表 |
| 告警与视频 | 告警规则、告警级别、告警数据、视频服务基础 CRUD |

V1.2 验收口径：可从数据对象或对象透视表生成至少一个图层、一个图表，并挂入业务主题。

### 10.4 V1.3：P2 高级配置和运维版本

V1.3 目标是覆盖剩余后台能力，重点是数据集成、导览、过滤器、字典、同步和高风险管理操作。

| 模块 | 需要补齐的 MCP Tool |
| --- | --- |
| 过滤器 | 过滤文件夹、过滤器、过滤项 CRUD |
| 首页配置 | 首页配置查询、更新、预览、重置 |
| 数据集成 | 数据库服务、接口服务、网关服务、字段绑定、预览数据 |
| 数据字典 | 字典文件夹、字典数据、导入导出 |
| 同步管理 | 标准孪生体同步、数据对象同步、任务状态和日志 |
| 用户与密码 | 用户查询、编辑、启停、重置密码 |
| 场景显示与导览 | 显示配置、导览、导览步骤、工具配置 |
| 固定资产 | 资产库、社区资产、粒子、材质等只读查询和预览 |

V1.3 验收口径：能完成高级配置读取、低风险写入、高风险操作确认、敏感字段脱敏和长任务轮询。

### 10.5 推荐排期

| 版本 | 时间建议 | 目标 | 关键交付 |
| --- | --- | --- | --- |
| V1.0 | 本周 | 最小闭环跑通 | 约 10 个 Tool + 一条演示脚本 |
| V1.1 | 下个节点 | P0 对象完整 | 场景、层级、点线面、类别类型/默认字段、类别、实例完整 CRUD |
| V1.2 | 第二个节点 | 数据可视化应用 | 数据对象、透视表、图层、图表、业务主题、告警视频 |
| V1.3 | 第三个节点 | 后台全量能力 | P2 高级配置、数据集成、导览、用户、固定资产 |

## 11. 典型闭环

### 11.1 P0 基础场景闭环

```text
get_twin_mcp_context
list_scenes / create_scene
create_scene_hierarchy
create_scene_poi / create_guide_route / create_key_area
list_twin_category_types
get_twin_category_default_fields
list_twin_category_folders / create_twin_category_folder
list_twin_categories / get_twin_category / create_twin_category
create_twin_ledger_field / create_twin_timeseries_field / create_twin_event_field
list_scene_twin_categories / add_scene_twin_category
get_scene_twin_category
begin_resource_upload -> commit_resource_upload
create_twin_instance
bind_twin_instance_resource
get_scene
list_twin_instances
```

### 11.2 P1 数据可视化闭环

```text
create_data_object_category
create_data_object_field
import_data_object_data
create_object_pivot
preview_object_pivot_result
create_layer
create_chart
create_business_theme
bind_theme_layer
bind_theme_chart
```

### 11.3 P1 告警视频闭环

```text
create_alarm_rule
create_alarm_level
list_alarm_data
create_video_service
create_video_resource
bind_video_alarm_data
bind_theme_object / bind_theme_layer / bind_theme_chart
```

### 11.4 P2 数据集成闭环

```text
create_database_service / create_api_service / create_gateway_server
test_database_service / test_api_service / test_gateway_server
create_database_table_link / create_receive_api / create_gateway_subscribe_api
preview_database_table_data
create_dictionary_data
run_data_object_sync
get_sync_task_status
get_sync_task_log
```

### 11.5 P2 导览配置闭环

```text
get_scene_display_config
update_scene_display_config
create_scene_tour
create_scene_tour_step
move_scene_tour_step
get_scene_tool_config
update_scene_tool_config
```

## 12. 待后端确认事项

| 编号 | 问题 | 影响 |
| --- | --- | --- |
| Q1 | 资源管理上传后返回字段是否统一为 `resource_code`、`asset_id` 或其他 ID | 影响资源引用模型 |
| Q2 | 场景、层级、POI、路线、区域的后端主键和树结构字段 | 影响 P0 场景对象 DTO |
| Q3 | 孪生体类别的台账/时序/事件字段在后端是否是三张表或统一字段表 | 影响字段工具拆分 |
| Q4 | 孪生体实例与类别、模型资源、空间位置的绑定接口 | 影响 `create_twin_instance` |
| Q5 | 删除场景、删除类别、删除实例、删除资源的后端是否有依赖校验 | 影响 `delete_* preview` |
| Q6 | 是否已有后端接口可预览资源、实例、点线面效果 | 影响 `preview_*` 工具 |
| Q7 | 对象透视表的配置结构和结果缓存方式 | 影响 P1 透视工具 |
| Q8 | 图层、图表、业务主题的绑定关系是否有独立接口 | 影响 P1 绑定工具 |
| Q9 | P2 数据集成接口中密码、Token、连接串的存储和脱敏机制 | 影响 P2 安全设计 |
| Q10 | 过滤器、导览、首页配置是否整包保存还是局部保存 | 影响 P2 写入合并策略 |
| Q11 | 固定资产是否允许租户级复制、引用、维护 | 影响固定资产 Tool 是否只读 |

## 13. 与场景自动生成系统 MCP 的差异

| 项 | 场景自动生成系统 MCP | 孪易标准版 MCP |
| --- | --- | --- |
| 主目标 | 自动生成、打包发布客户预览场景 | 配置孪易标准版后台对象，支撑后续智能化搭建 |
| 对象根域 | 项目工程 | 租户 + 场景 |
| P0 重点 | 项目、资源、业务对象、生成发布 | 场景、层级、点线面、孪生体类别、孪生体实例、资源管理 |
| 生成入口 | `submit_project_generation` + `get_project_state` | P0 暂不定义生成发布入口，以配置结果可查为闭环 |
| 业务对象 | `scene_business_object` 聚合 Node/Setting | 按孪易对象粒子化 Tool 拆分 |
| Skill | 文档提到 MCP 上层是 Skill，但该文档主要设计 MCP Tools | 当前阶段只定 MCP，不生成 Skill |

## 14. P0 验收清单

| 编号 | 验收项 |
| --- | --- |
| V01 | MCP Server 可登录孪易后端并获取 Token |
| V02 | 可读取当前租户信息，且不跨租户操作 |
| V03 | 可创建、查询、编辑、删除场景 |
| V04 | 可创建、查询、编辑、删除场景层级 |
| V05 | 可创建、查询、编辑、删除 POI 点位 |
| V06 | 可创建、查询、编辑、删除指引路线 |
| V07 | 可创建、查询、编辑、删除重点区域 |
| V08 | 可查询可用类别元类型和默认字段，并可查询、创建、编辑、删除租户级已创建孪生体类别 |
| V09 | 可创建、查询、编辑、删除类别台账/时序/事件字段 |
| V10 | 可上传并查询资源管理中的模型、图片、视频、媒体、数据表资源 |
| V11 | 可创建孪生体实例，并绑定类别、资源和空间位置 |
| V12 | 可查询场景中的孪生体实例列表和详情 |
| V13 | 删除类工具在 `preview` 模式返回影响范围，不写后端 |
| V14 | 删除类工具在缺少 `confirm=true` 时拒绝执行 |
| V15 | 所有工具返回统一 `status/summary/data/warnings/affected_objects/next_actions` 结构 |
