# 孪易标准版 MCP 接口实施清单

> 本清单用于跟踪孪易标准版 MCP Server 需要实现的 Tool、对应后端接口映射和完成状态。  
> 状态说明：`已实现` = 代码中已有 Tool；`待实现` = 设计已明确但代码未实现；`待确认` = 对象已规划但后端接口或 DTO 仍需校准。

## P0 / V1.0-V1.1 基础闭环接口

| 序号 | 中文名称 | 英文名称 | 后端接口映射 | 完成状态 |
| ---: | --- | --- | --- | --- |
| 1 | 登录孪易后端 | `login_twin_backend` | `POST /v1/login` | 已实现 |
| 2 | 获取 MCP 上下文 | `get_twin_mcp_context` | `GET /v1/my/info`；未登录时返回本地配置摘要 | 已实现 |
| 3 | 获取场景根资产文件夹 | `get_location_asset_folder_root` | `GET /v1/{operationalData}/Location/assetFolder/root` | 已实现 |
| 4 | 查询场景列表 | `list_scenes` | `GET /v1/{operationalData}/assetFolders/{assetFolderID}/location` | 已实现 |
| 5 | 创建场景 | `create_scene` | `POST /v1/{operationalData}/assetFolders/{assetFolderID}/location/add` | 已实现 |
| 6 | 查询场景详情 | `get_scene` | `GET /v1/{operationalData}/location/{locationId}` | 已实现 |
| 7 | 编辑场景信息 | `update_scene` | `POST /v1/{operationalData}/location/{locationId}/edit` | 已实现 |
| 8 | 重命名场景 | `rename_scene` | `POST /v1/{operationalData}/location/{locationId}/rename` | 已实现 |
| 9 | 删除场景 | `delete_scene` | `POST /v1/{operationalData}/location/{locationId}/delete` | 已实现 |
| 10 | 复制场景 | `copy_scene` | `POST /v1/location/{locationId}/copy`；行业/模板导入用 `POST /v1/copyIndustryData/location/{locationId}/folder/{folderId}` | 已实现 |
| 11 | 导出场景 ZIP | `export_scene` | `HEAD /v1/location/{locationId}/exportToZip`；`POST /v1/location/{locationId}/exportToZip`；`POST /v1/location/{locationId}/getExportZip` | 待实现 |
| 12 | 导入场景包 | `import_scene` | `POST /v1/assetFolders/{assetFolderID}/location/import`；更新已有场景用 `POST /v1/location/{locationId}/import` | 待实现 |
| 13 | 创建场景层级 | `create_scene_hierarchy` | `POST /v1/{operationalData}/location/{locationId}/level/add` | 已实现 |
| 14 | 查询场景层级列表 | `list_scene_hierarchies` | `GET /v1/{operationalData}/location/{locationId}/level` | 已实现 |
| 15 | 查询单个场景层级 | `get_scene_hierarchy` | `GET /v1/{operationalData}/location/{locationId}/level` 后由 MCP 按 `levelID` 过滤 | 已实现 |
| 16 | 编辑场景层级 | `update_scene_hierarchy` | `POST /v1/{operationalData}/location/{locationId}/level/{levelId}/edit` | 已实现 |
| 17 | 重命名场景层级 | `rename_scene_hierarchy` | `POST /v1/{operationalData}/location/{locationId}/level/{levelId}/edit` | 已实现 |
| 18 | 删除场景层级 | `delete_scene_hierarchy` | `POST /v1/{operationalData}/location/{locationId}/level/delete` | 已实现 |
| 19 | 调整场景层级顺序 | `move_scene_hierarchy` | `POST /v1/{operationalData}/location/{locationId}/level/{levelId}/{sortType}` | 已实现 |
| 20 | 创建兴趣点位 POI | `create_scene_poi` | `POST /v1/{operationalData}/location/{locationId}/level/{levelId}/position/add` | 已实现 |
| 21 | 查询兴趣点位列表 | `list_scene_pois` | `GET /v1/{operationalData}/location/{locationId}/level/{levelId}/position` | 已实现 |
| 22 | 查询兴趣点位详情 | `get_scene_poi` | `GET /v1/{operationalData}/location/{locationId}/level/{levelId}/position` 后由 MCP 按 `positionID` 过滤 | 已实现 |
| 23 | 编辑兴趣点位 | `update_scene_poi` | `POST /v1/{operationalData}/location/{locationId}/position/{positionId}/edit` | 已实现 |
| 24 | 重命名兴趣点位 | `rename_scene_poi` | `POST /v1/{operationalData}/location/{locationId}/position/{positionId}/edit` | 已实现 |
| 25 | 删除兴趣点位 | `delete_scene_poi` | `POST /v1/{operationalData}/location/{locationId}/position/delete` | 已实现 |
| 26 | 创建指引路线 | `create_guide_route` | `POST /v1/{operationalData}/location/{locationId}/level/{levelId}/path/add` | 已实现 |
| 27 | 查询指引路线列表 | `list_guide_routes` | `GET /v1/{operationalData}/location/{locationId}/level/{levelId}/path` | 已实现 |
| 28 | 查询指引路线详情 | `get_guide_route` | `GET /v1/{operationalData}/location/{locationId}/level/{levelId}/path` 后由 MCP 按 `pathID` 过滤 | 已实现 |
| 29 | 编辑指引路线 | `update_guide_route` | `POST /v1/{operationalData}/location/{locationId}/path/{pathId}/edit` | 已实现 |
| 30 | 重命名指引路线 | `rename_guide_route` | `POST /v1/{operationalData}/location/{locationId}/path/{pathId}/edit` | 已实现 |
| 31 | 删除指引路线 | `delete_guide_route` | `POST /v1/{operationalData}/location/{locationId}/path/delete` | 已实现 |
| 32 | 创建重点区域 | `create_key_area` | `POST /v1/{operationalData}/location/{locationId}/level/{levelId}/region/add` | 已实现 |
| 33 | 查询重点区域列表 | `list_key_areas` | `GET /v1/{operationalData}/location/{locationId}/level/{levelId}/region` | 已实现 |
| 34 | 查询重点区域详情 | `get_key_area` | `GET /v1/{operationalData}/location/{locationId}/level/{levelId}/region` 后由 MCP 按 `regionID` 过滤 | 已实现 |
| 35 | 编辑重点区域 | `update_key_area` | `POST /v1/{operationalData}/location/{locationId}/region/{regionId}/edit` | 已实现 |
| 36 | 重命名重点区域 | `rename_key_area` | `POST /v1/{operationalData}/location/{locationId}/region/{regionId}/edit` | 已实现 |
| 37 | 删除重点区域 | `delete_key_area` | `POST /v1/{operationalData}/location/{locationId}/region/delete` | 已实现 |

## P0 / V1.1 租户类别、场景类别配置、实例和资源接口

> 范围说明：`list_twin_category_*`、`create_twin_category`、`update_twin_category` 这一组当前指 **租户级孪生体类别库**，核心 ID 是 `twinCategoryID`，不带 `locationId`。场景内创建实例前，必须先把租户类别加入指定场景，得到场景级 `twinCategoryConfigID`；实例、模型显示、台账/时序/事件数据写入都应使用 `twinCategoryConfigID`。
>
> 实例台账写入说明：`ledger_data_json` 必须使用 `list_twin_ledger_fields` 返回的后端字段名；MCP 不自动生成位置/经纬度字段；写入实例时会按 `granularity_type + granularity` 归一化实例时间：Year=当前年+指定月1日，Month=当前年月+指定日，Day=当前年月日+指定小时。

| 序号 | 中文名称 | 英文名称 | 后端接口映射 | 完成状态 |
| ---: | --- | --- | --- | --- |
| 38 | 查询场景级孪生体类别文件夹 | `list_scene_twin_category_folders` | `GET /v1/{operationalData}/location/{locationId}/TwinCategory/assetFolder/root`；再用根 `folderID` 查询 `/assetFolder/{parentFolderId}` | 已实现 |
| 39 | 创建场景级孪生体类别文件夹 | `create_scene_twin_category_folder` | `POST /v1/{operationalData}/location/{locationId}/TwinCategory/assetFolder/{parentFolderId}/add` | 已实现 |
| 40 | 编辑场景级孪生体类别文件夹 | `update_scene_twin_category_folder` | `POST /v1/{operationalData}/location/{locationId}/TwinCategory/assetFolder/{folderId}/edit`；只修改文件夹名称，禁止编辑根文件夹 | 已实现 |
| 41 | 重命名场景级孪生体类别文件夹 | `rename_scene_twin_category_folder` | 同 `update_scene_twin_category_folder`，作为重命名语义别名 | 已实现 |
| 42 | 删除场景级孪生体类别文件夹 | `delete_scene_twin_category_folder` | `POST /v1/{operationalData}/location/{locationId}/TwinCategory/assetFolder/{folderId}/delete`；禁止删除根文件夹 | 已实现 |
| 43 | 查询场景文件夹下孪生体类别配置 | `list_scene_twin_categories` | `POST /v1/{operationalData}/location/{locationId}/folder/{folderId}/twinCategory`；返回 `twinCategoryConfigID` 与租户源 `twinCategoryID` | 已实现 |
| 44 | 添加场景孪生体类别配置 | `add_scene_twin_category` | `POST /v1/{operationalData}/location/{locationId}/folder/{folderId}/twinCategory/{twinCategoryId}`；输入租户级 `twinCategoryID`，生成场景级 `twinCategoryConfigID`；未显式传粒度时默认 `granularity=1`、`granularityType=Year` | 已实现 |
| 45 | 查询场景孪生体类别配置详情 | `get_scene_twin_category` | `GET /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/details` | 已实现 |
| 46 | 编辑场景孪生体类别配置名称 | `update_scene_twin_category` | `POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/editName` | 已实现 |
| 47 | 重命名场景孪生体类别配置 | `rename_scene_twin_category` | 同 `update_scene_twin_category`，作为重命名语义别名 | 已实现 |
| 48 | 删除场景孪生体类别配置 | `delete_scene_twin_category` | `POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/delete`；删除场景配置及数据，不删除租户级模板 | 已实现 |
| 49 | 查询孪生体类别元类型枚举 | `list_twin_category_types` | 孪易标准版 MCP 内置 13 类可创建 `categoryType` 白名单：现有 12 类 + `DataObject`；部署不依赖 `input` 目录 | 已实现 |
| 50 | 查询租户级类别类型默认字段 | `get_twin_category_default_fields` | `GET /v1/twinCategory/defaultField/{categoryType}` | 已实现 |
| 51 | 创建租户级孪生体类别文件夹 | `create_twin_category_folder` | `POST /v1/{operationalData}/{assetFolderType}/assetFolder/{parentFolderId}/add`，`assetFolderType=TwinCategory` | 已实现 |
| 52 | 查询租户级孪生体类别文件夹 | `list_twin_category_folders` | 不传 `parent_folder_id` 时先 `GET /v1/{operationalData}/TwinCategory/assetFolder/root` 取根 `folderID`，再自动 `GET /v1/{operationalData}/TwinCategory/assetFolder/{parentFolderId}` 查询根下子文件夹；显式传 ID 时直接查子级 | 已实现 |
| 53 | 编辑租户级孪生体类别文件夹 | `update_twin_category_folder` | `POST /v1/{operationalData}/TwinCategory/assetFolder/{folderId}/edit`；只修改文件夹名称，禁止编辑 TwinCategory 根文件夹 | 已实现 |
| 54 | 重命名租户级孪生体类别文件夹 | `rename_twin_category_folder` | 同 `update_twin_category_folder`，作为重命名语义别名 | 已实现 |
| 55 | 删除租户级孪生体类别文件夹 | `delete_twin_category_folder` | `POST /v1/{operationalData}/TwinCategory/assetFolder/{folderId}/delete`；禁止删除 TwinCategory 根文件夹 | 已实现 |
| 56 | 查询租户级已创建孪生体类别列表 | `list_tenant_twin_categories` / `list_twin_categories` | `POST /v1/{operationalData}/twinCategory/folder/{folderId}`；不传 `folder_id` 默认查 TwinCategory 根及所有子文件夹；请求不传 `twinCategoryType` 过滤，避免漏掉 `DataObject`；返回租户类别 `twinCategoryID` | 已实现 |
| 57 | 查询租户级孪生体类别详情 | `get_twin_category` | `GET /v1/{operationalData}/twinCategory/{twinCategoryId}` | 已实现 |
| 58 | 创建租户级孪生体类别 | `create_twin_category` | 先校验 `categoryType` 并拉取默认字段；再 `POST /v1/{operationalData}/twinCategory/Save` | 已实现 |
| 59 | 编辑租户级孪生体类别 | `update_twin_category` | `POST /v1/{operationalData}/twinCategory/Save`；系统字段用 `POST /v1/{operationalData}/twinCategory/{twinCategoryID}/updateSystemFieldDefinitions` | 已实现 |
| 60 | 重命名租户级孪生体类别 | `rename_twin_category` | 复用 `POST /v1/{operationalData}/twinCategory/Save`，只修改名称 | 已实现 |
| 61 | 删除租户级孪生体类别 | `delete_twin_category` | `POST /v1/{operationalData}/twinCategory/delete`；请求体为 `string[]` | 已实现 |
| 62 | 复制租户级孪生体类别 | `copy_twin_category` | `POST /v1/twinCategorys/copy`；请求体为 `string[]`；可在复制成功并解析新 ID 后移动到目标文件夹 | 已实现 |
| 63 | 移动租户级孪生体类别 | `move_twin_category` | `POST /v1/{operationalData}/twinCategory/move/folder/{folderId}`；请求体为 `string[]` | 已实现 |
| 64 | 创建台账字段 | `create_twin_ledger_field` | 无独立字段新增接口；通过 `POST /v1/{operationalData}/twinCategory/Save` 整包保存字段定义 | 待实现 |
| 65 | 查询场景台账字段 | `list_twin_ledger_fields` | `GET /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/field`；输入场景级 `twinCategoryConfigID` | 已实现 |
| 66 | 编辑台账字段 | `update_twin_ledger_field` | 无独立字段编辑接口；通过 `POST /v1/{operationalData}/twinCategory/Save` 或 `updateSystemFieldDefinitions` 整包保存 | 待实现 |
| 67 | 删除台账字段 | `delete_twin_ledger_field` | 无独立字段删除接口；通过 `POST /v1/{operationalData}/twinCategory/Save` 整包保存字段定义 | 待实现 |
| 68 | 创建时序字段 | `create_twin_timeseries_field` | 无独立字段新增接口；通过 `POST /v1/{operationalData}/twinCategory/Save` 整包保存字段定义 | 待实现 |
| 69 | 查询时序字段 | `list_twin_timeseries_fields` | 场景类别时序字段：`GET /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/timeSeriesField`；租户类别从详情/Save DTO 校准 | 待实现 |
| 70 | 编辑时序字段 | `update_twin_timeseries_field` | 无独立字段编辑接口；通过 `POST /v1/{operationalData}/twinCategory/Save` 整包保存字段定义 | 待实现 |
| 71 | 删除时序字段 | `delete_twin_timeseries_field` | 无独立字段删除接口；通过 `POST /v1/{operationalData}/twinCategory/Save` 整包保存字段定义 | 待实现 |
| 72 | 创建事件字段 | `create_twin_event_field` | 无独立字段新增接口；通过 `POST /v1/{operationalData}/twinCategory/Save` 整包保存字段定义 | 待实现 |
| 73 | 查询事件字段 | `list_twin_event_fields` | 场景类别事件字段：`GET /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/eventField`；租户类别从详情/Save DTO 校准 | 待实现 |
| 74 | 编辑事件字段 | `update_twin_event_field` | 无独立字段编辑接口；通过 `POST /v1/{operationalData}/twinCategory/Save` 整包保存字段定义 | 待实现 |
| 75 | 删除事件字段 | `delete_twin_event_field` | 无独立字段删除接口；通过 `POST /v1/{operationalData}/twinCategory/Save` 整包保存字段定义 | 待实现 |
| 76 | 查询类别模型配置 | `get_twin_category_model_config` | 租户级：`GET /v1/{operationalData}/twinCategory/{twinCategoryId}/model`；场景级：`GET /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/model` | 待实现 |
| 77 | 修改类别模型配置 | `update_twin_category_model_config` | 场景级：`POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/model/edit`；租户级通过 `Save` DTO 待校准 | 待实现 |
| 78 | 查询类别基础设置 | `get_twin_category_base_setting` | 租户级：`GET /v1/{operationalData}/twinCategory/{twinCategoryId}/setting`；场景级：`GET /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/setting` | 待实现 |
| 79 | 修改类别基础设置 | `update_twin_category_base_setting` | 场景级：`POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/setting`；租户级通过 `Save` DTO 待校准 | 待实现 |
| 80 | 查询类别选中配置 | `get_twin_category_selection_setting` | `GET /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/setting` 后读取选中相关配置 | 待实现 |
| 81 | 修改类别选中配置 | `update_twin_category_selection_setting` | `POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/setting` 合并写入选中配置 | 待实现 |
| 82 | 查询类别标签设置 | `get_twin_category_label_setting` | `GET /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/setting` 后读取标签配置 | 待实现 |
| 83 | 修改类别标签设置 | `update_twin_category_label_setting` | `POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/setting` 合并写入标签配置 | 待实现 |
| 84 | 查询类别可见设置 | `get_twin_category_visibility_setting` | `GET /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/displayMode` 或 `setting` | 待实现 |
| 85 | 修改类别可见设置 | `update_twin_category_visibility_setting` | `POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/level/{levelId}` 或 `setting` | 待实现 |
| 86 | 创建场景孪生体实例 | `create_twin_instance` | `POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/data/add`；批量用 `/data/batchAdd`；preview/execute 预校验 `ledger_data_json` 并按粒度归一化实例时间 | 已实现 |
| 87 | 查询场景孪生体实例列表 | `list_twin_instances` | 类别内：`POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/data`；全场景：`POST /v1/{operationalData}/location/{locationId}/data` 或 `/twinCategoryData` | 已实现 |
| 88 | 查询场景孪生体实例详情 | `get_twin_instance` | `POST /v1/{operationalData}/location/{locationId}/data` 或 `/twinCategoryData` 后由 MCP 按实例 ID 过滤 | 已实现 |
| 89 | 编辑场景孪生体实例 | `update_twin_instance` | `POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/data/batchEdit`；preview/execute 预校验台账 JSON 和时间格式 | 已实现 |
| 90 | 删除场景孪生体实例 | `delete_twin_instance` | `POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/data/delete`；清空用 `/data/deleteAll` | 已实现 |
| 91 | 复制场景孪生体实例 | `copy_twin_instance` | 后端未发现独立复制接口；MCP 读源实例后调用 `/data/add` 创建副本 | 待实现 |
| 92 | 移动场景孪生体实例 | `move_twin_instance` | 通过 `/data/batchEdit` 更新层级、区域或位置内容 | 待实现 |
| 93 | 绑定实例资源 | `bind_twin_instance_resource` | 视频/跳转可能使用 `POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/updateVideoAndJumpLocation` 或 `/updateVideo` | 待实现 |
| 94 | 解绑实例资源 | `unbind_twin_instance_resource` | 同绑定接口：`POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/updateVideoAndJumpLocation` 或 `/updateVideo`，传空/移除视频与跳转字段 | 待实现 |
| 95 | 预览实例上下文 | `preview_twin_instance` | 后端未发现独立预览接口；MCP 返回场景上下文和实例详情 | 待实现 |
| 96 | 写入实例台账数据 | `upsert_twin_instance_ledger_data` | 新增：`POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/data/add`；批量新增 `/data/batchAdd`；更新 `/data/batchEdit`；支持 `granularity_type=Year/Month/Day` 与 `granularity`，时间字段统一归一化为 `yyyy-MM-dd HH:mm:ss` | 已实现 |
| 97 | 查询实例台账数据 | `list_twin_instance_ledger_data` | `POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/data`；全场景 `/location/{locationId}/data` | 已实现 |
| 98 | 删除实例台账数据 | `delete_twin_instance_ledger_data` | `POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/data/delete` | 已实现 |
| 99 | 写入实例时序数据 | `upsert_twin_instance_timeseries_data` | `POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/addTimeSeriesData` | 待实现 |
| 100 | 查询实例时序数据 | `list_twin_instance_timeseries_data` | `GET/POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/typeDistinguish/{typeDistinguish}`，`typeDistinguish=TwinTimeSeries` | 待实现 |
| 101 | 删除实例时序数据 | `delete_twin_instance_timeseries_data` | `POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/deleteTimeSeriesData` | 待实现 |
| 102 | 写入实例事件数据 | `upsert_twin_instance_event_data` | `POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/addEventData` | 待实现 |
| 103 | 查询实例事件数据 | `list_twin_instance_event_data` | `GET/POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/typeDistinguish/{typeDistinguish}`，`typeDistinguish=TwinEvent` | 待实现 |
| 104 | 删除实例事件数据 | `delete_twin_instance_event_data` | `POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/deleteEventData` | 待实现 |
| 105 | 创建资源文件夹 | `create_resource_folder` | `POST /v1/{operationalData}/{assetFolderType}/assetFolder/{parentFolderId}/add` | 待实现 |
| 106 | 查询资源文件夹 | `list_resource_folders` | 根：`GET /v1/{operationalData}/{assetFolderType}/assetFolder/root`；子级：`GET /v1/{operationalData}/{assetFolderType}/assetFolder/{parentFolderId}` | 待实现 |
| 107 | 编辑资源文件夹 | `update_resource_folder` | `POST /v1/{operationalData}/{assetFolderType}/assetFolder/{folderId}/edit` | 待实现 |
| 108 | 删除资源文件夹 | `delete_resource_folder` | `POST /v1/{operationalData}/{assetFolderType}/assetFolder/{folderId}/delete` | 待实现 |
| 109 | 开始上传资源 | `begin_resource_upload` | 标准 begin 未发现；模型 TGM 用 `POST /v1/asset/folder/{folderID}/tgm/import`；媒体用 `POST /v1/{operationalData}/assetLibrary/asset/importMediaFiles`；数据表用 `POST /v1/{operationalData}/dataSourceFolders/{dataSourceFolderID}/dataSource/upload` | 待实现 |
| 110 | 完成资源上传 | `commit_resource_upload` | 与具体上传接口合并执行；未发现独立 commit 接口 | 待实现 |
| 111 | 取消资源上传 | `cancel_resource_upload` | 待确认 | 待确认 |
| 112 | 查询资源列表 | `list_resources` | 文件夹内：`POST /v1/{operationalData}/folder/{folderID}/assets`；全局：`POST /v1/{operationalData}/assets` | 待实现 |
| 113 | 查询资源详情 | `get_resource` | `GET /v1/{operationalData}/asset/{assetID}/detail` 或 `GET /v1/asset/{assetID}/detail` | 待实现 |
| 114 | 编辑资源元数据 | `update_resource` | 模型：`POST /v1/asset/{assetID}/editModel`；其他资源类型未发现统一编辑接口，需按资源类型分别校准 | 待实现 |
| 115 | 删除资源 | `delete_resource` | `POST /v1/assets/delete` | 待实现 |
| 116 | 预览资源 | `preview_resource` | `GET /v1/assetLibrary/{assetUsage}/asset/{assetID}/{expandName}/{fileName}` | 待实现 |
| 117 | 绑定资源 | `bind_resource` | 无统一接口；按目标对象配置写入 | 待确认 |
| 118 | 解绑资源 | `unbind_resource` | 无统一接口；按目标对象配置移除 | 待确认 |

## P1 / V1.2 数据和可视化接口

| 序号 | 中文名称 | 英文名称 | 后端接口映射 | 完成状态 |
| ---: | --- | --- | --- | --- |
| 119 | 创建数据对象类别 | `create_data_object_category` | `POST /v1/{operationalData}/location/{locationId}/folder/{folderId}/dataObject/add` | 待实现 |
| 120 | 查询数据对象类别 | `list_data_object_categories` | `POST /v1/{operationalData}/location/{locationId}/dataObject`；按文件夹：`POST /v1/{operationalData}/location/{locationId}/folder/{folderId}/dataObject`；全部：`GET /v1/{operationalData}/location/{locationId}/dataObject/all` | 待实现 |
| 121 | 查询数据对象类别详情 | `get_data_object_category` | `GET /v1/{operationalData}/location/{locationId}/dataObject/{twinCategoryID}/details` | 待实现 |
| 122 | 编辑数据对象类别 | `update_data_object_category` | 重命名：`POST /v1/{operationalData}/location/{locationId}/dataObject/{twinCategoryID}/editName`；字段更新：`POST /v1/{operationalData}/location/{locationId}/dataObject/{twinCategoryID}/initialization` | 待实现 |
| 123 | 删除数据对象类别 | `delete_data_object_category` | `POST /v1/{operationalData}/location/{locationId}/dataObject/{twinCategoryID}/delete` | 待实现 |
| 124 | 创建数据对象字段 | `create_data_object_field` | 无独立字段新增接口；通过数据对象字段定义整包更新/初始化接口处理：`POST /v1/{operationalData}/location/{locationId}/dataObject/{twinCategoryID}/initialization` | 待实现 |
| 125 | 查询数据对象字段 | `list_data_object_fields` | `GET /v1/{operationalData}/location/{locationId}/dataObject/{twinCategoryID}/field` | 待实现 |
| 126 | 编辑数据对象字段 | `update_data_object_field` | 无独立字段编辑接口；通过 `initialization` 或类别整包保存处理 | 待实现 |
| 127 | 删除数据对象字段 | `delete_data_object_field` | 无独立字段删除接口；通过 `initialization` 或类别整包保存处理 | 待实现 |
| 128 | 导入数据对象数据 | `import_data_object_data` | `POST /v1/{operationalData}/location/{locationId}/dataObject/{twinCategoryID}/importData` | 待实现 |
| 129 | 新增或更新数据对象数据 | `upsert_data_object_data` | 新增：`POST /v1/{operationalData}/location/{locationId}/dataObject/{twinCategoryID}/addData`；未发现独立更新接口，可先按主键删除后新增或通过导入覆盖策略处理 | 待实现 |
| 130 | 查询数据对象数据 | `list_data_object_data` | `POST /v1/{operationalData}/location/{locationId}/dataObject/{twinCategoryID}/queryData`；数量：`queryDataCount`；字段去重：`queryColumnDistinctData` | 待实现 |
| 131 | 删除数据对象数据 | `delete_data_object_data` | `POST /v1/{operationalData}/location/{locationId}/dataObject/{twinCategoryID}/deleteData` | 待实现 |
| 132 | 创建对象透视表 | `create_object_pivot` | `POST /v1/{operationalData}/location/{locationId}/objectPerspective/add` | 待实现 |
| 133 | 查询对象透视表 | `list_object_pivots` | `POST /v1/{operationalData}/location/{locationId}/objectPerspective`；全部：`GET /v1/{operationalData}/location/{locationId}/objectPerspective/all` | 待实现 |
| 134 | 查询对象透视表详情 | `get_object_pivot` | `POST /v1/{operationalData}/location/{locationId}/objectPerspective/{objectPerspectiveID}`；字段：`GET /v1/{operationalData}/location/{locationId}/objectPerspective/{objectPerspectiveID}/field` | 待实现 |
| 135 | 编辑对象透视表 | `update_object_pivot` | `POST /v1/{operationalData}/location/{locationId}/objectPerspective/{objectPerspectiveID}/edit`；重命名：`POST /v1/{operationalData}/location/{locationId}/objectPerspective/{objectPerspectiveID}/editName` | 待实现 |
| 136 | 删除对象透视表 | `delete_object_pivot` | `POST /v1/{operationalData}/location/{locationId}/objectPerspective/{objectPerspectiveID}/delete` | 待实现 |
| 137 | 预览对象透视结果 | `preview_object_pivot_result` | `POST /v1/{operationalData}/location/{locationId}/objectPerspective/{objectPerspectiveID}/queryData`；数量：`queryDataCount`；字段去重：`queryColumnDistinctData` | 待实现 |
| 138 | 刷新对象透视结果 | `refresh_object_pivot_result` | `POST /v1/{operationalData}/location/{locationId}/objectPerspective/{objectPerspectiveID}/generateData`；状态：`GET /v1/{operationalData}/location/{locationId}/objectPerspective/{objectPerspectiveID}/generateData/status` | 待实现 |
| 139 | 创建图层文件夹 | `create_layer_folder` | `POST /v1/{operationalData}/location/{locationId}/{locationFolderType}/assetFolder/{parentFolderId}/add`，`locationFolderType=Layer` | 待实现 |
| 140 | 创建图层 | `create_layer` | `POST /v1/{operationalData}/location/{locationId}/layer/folder/{layerFolderId}/add`；模板图层：`/defaultLayer/{defaultLayerId}/add/{twinCategoryConfigID}` | 待实现 |
| 141 | 查询图层列表 | `list_layers` | 文件夹：`GET /v1/{operationalData}/location/{locationId}/layer/folder/{layerFolderId}`；全部：`POST /v1/{operationalData}/location/{locationId}/layer` | 待实现 |
| 142 | 查询图层详情 | `get_layer` | `GET /v1/{operationalData}/location/{locationId}/layer/{layerId}` | 待实现 |
| 143 | 编辑图层 | `update_layer` | `POST /v1/{operationalData}/location/{locationId}/layer/{layerId}/edit` | 待实现 |
| 144 | 删除图层 | `delete_layer` | `POST /v1/{operationalData}/location/{locationId}/layer/{layerId}/delete` | 待实现 |
| 145 | 创建图表文件夹 | `create_chart_folder` | `POST /v1/{operationalData}/location/{locationId}/{locationFolderType}/assetFolder/{parentFolderId}/add`，`locationFolderType=Chart` | 待实现 |
| 146 | 创建图表 | `create_chart` | `POST /v1/{operationalData}/location/{locationId}/chart/folder/{chartFolderId}/add`；默认图表：`/defaultChart/{defaultChartId}/add/{twinCategoryConfigID}` | 待实现 |
| 147 | 查询图表列表 | `list_charts` | `GET /v1/{operationalData}/location/{locationId}/chart/folder/{chartFolderId}` | 待实现 |
| 148 | 查询图表详情 | `get_chart` | `GET /v1/{operationalData}/location/{locationId}/chart/{chartId}` | 待实现 |
| 149 | 编辑图表 | `update_chart` | `POST /v1/{operationalData}/location/{locationId}/chart/{chartId}/edit` | 待实现 |
| 150 | 删除图表 | `delete_chart` | `POST /v1/{operationalData}/location/{locationId}/chart/{chartId}/delete` | 待实现 |
| 151 | 创建业务主题 | `create_business_theme` | 未发现独立创建接口；目前仅发现主题名查询接口 | 待实现 |
| 152 | 查询业务主题 | `list_business_themes` | `GET /webApi/getAllThemeName` | 待实现 |
| 153 | 查询业务主题详情 | `get_business_theme` | 未发现独立详情接口；可先通过主题名列表和相关对象配置反查 | 待实现 |
| 154 | 编辑业务主题 | `update_business_theme` | 未发现独立编辑接口；可能随图层、图表、对象配置中的业务主题字段保存 | 待实现 |
| 155 | 删除业务主题 | `delete_business_theme` | 未发现独立删除接口；可能随引用对象配置移除 | 待实现 |
| 156 | 绑定主题对象 | `bind_theme_object` | 未发现独立绑定接口；可能写入孪生体类别/实例或对象配置中的业务主题字段 | 待实现 |
| 157 | 解绑主题对象 | `unbind_theme_object` | 未发现独立解绑接口；可能从孪生体类别/实例或对象配置中移除业务主题字段 | 待实现 |
| 158 | 绑定主题图层 | `bind_theme_layer` | 未发现独立绑定接口；可通过 `POST /v1/{operationalData}/location/{locationId}/layer/{layerId}/edit` 写入主题相关配置 | 待实现 |
| 159 | 解绑主题图层 | `unbind_theme_layer` | 未发现独立解绑接口；可通过 `POST /v1/{operationalData}/location/{locationId}/layer/{layerId}/edit` 移除主题相关配置 | 待实现 |
| 160 | 绑定主题图表 | `bind_theme_chart` | 未发现独立绑定接口；可通过 `POST /v1/{operationalData}/location/{locationId}/chart/{chartId}/edit` 写入主题相关配置 | 待实现 |
| 161 | 解绑主题图表 | `unbind_theme_chart` | 未发现独立解绑接口；可通过 `POST /v1/{operationalData}/location/{locationId}/chart/{chartId}/edit` 移除主题相关配置 | 待实现 |
| 162 | 创建告警文件夹 | `create_alarm_folder` | `POST /v1/{operationalData}/location/{locationId}/{locationFolderType}/assetFolder/{parentFolderId}/add`，`locationFolderType=Alarm` | 待实现 |
| 163 | 创建告警规则 | `create_alarm_rule` | `POST /v1/{operationalData}/location/{locationId}/alarm/folder/{alarmFolderId}/add`；模板告警：`POST /v1/{operationalData}/location/{locationId}/alarm/folder/{alarmFolderId}/defaultAlarm/{defaultAlarmId}/add/{twinCategoryConfigID}` | 待实现 |
| 164 | 查询告警规则 | `list_alarm_rules` | 全部：`GET /v1/{operationalData}/location/{locationId}/alarm/all`；文件夹：`GET /v1/{operationalData}/location/{locationId}/alarm/folder/{alarmFolderId}` | 待实现 |
| 165 | 查询告警规则详情 | `get_alarm_rule` | `GET /v1/{operationalData}/location/{locationId}/alarm/{alarmId}` | 待实现 |
| 166 | 编辑告警规则 | `update_alarm_rule` | `POST /v1/{operationalData}/location/{locationId}/alarm/{alarmId}/edit` | 待实现 |
| 167 | 删除告警规则 | `delete_alarm_rule` | `POST /v1/{operationalData}/location/{locationId}/alarm/{alarmId}/delete` | 待实现 |
| 168 | 配置告警级别 | `create_alarm_level` | 查询：`GET /v1/{operationalData}/location/{locationId}/alarmLevelConfig`；保存：`POST /v1/{operationalData}/location/{locationId}/alarmLevelConfig` | 待实现 |
| 169 | 编辑告警级别 | `update_alarm_level` | 查询：`GET /v1/{operationalData}/location/{locationId}/alarmLevelConfig`；保存：`POST /v1/{operationalData}/location/{locationId}/alarmLevelConfig` | 待实现 |
| 170 | 查询告警数据 | `list_alarm_data` | `POST /v1/{operationalData}/location/{locationId}/alarmData`；未确认提醒：`GET /v1/{operationalData}/location/{locationId}/alarmData/notConfirm`；字段：`POST /v1/{operationalData}/location/{locationId}/alarmDataColumn` | 待实现 |
| 171 | 查询告警数据详情 | `get_alarm_data` | 未发现独立详情接口；MCP 可通过 `POST /v1/{operationalData}/location/{locationId}/alarmData` 查询后按 `alarmDataID` 过滤 | 待实现 |
| 172 | 创建视频服务 | `create_video_service` | `POST /v1/{operationalData}/videoServerResource/videoServerFolderId/{videoServerFolderId}/addOrUpedit` | 待实现 |
| 173 | 查询视频服务 | `list_video_services` | `POST /v1/{operationalData}/videoServerResource/videoServerFolderId/{videoServerFolderId}/videoServers` | 待实现 |
| 174 | 查询视频服务详情 | `get_video_service` | `POST /v1/{operationalData}/videoServerResource/videoServers/{videoServerId}/detail` | 待实现 |
| 175 | 编辑视频服务 | `update_video_service` | `POST /v1/{operationalData}/videoServerResource/videoServerFolderId/{videoServerFolderId}/addOrUpedit` | 待实现 |
| 176 | 删除视频服务 | `delete_video_service` | `POST /v1/{operationalData}/videoServerResource/videoServers/delete` | 待实现 |
| 177 | 创建视频资源 | `create_video_resource` | 本地图片/视频资产：`POST /v1/{operationalData}/assetLibrary/assetFolders/{assetFolderID}/createLocalPictureOrVideo`；IVS 视频资源：`POST /v1/{operationalData}/videoServerResource/videoServerFolderId/{videoServerFolderId}/addIVSVideoResource` | 待实现 |
| 178 | 绑定视频告警数据 | `bind_video_alarm_data` | IVS 告警配置：`GET /v1/{operationalData}/location/{locationId}/alarmIVSConfig`；启停：`POST /v1/{operationalData}/location/{locationId}/alarmIVSEnable`；行为规则：`POST /v1/{operationalData}/location/{locationId}/alarmIVSBehaviorRule` | 待实现 |

## P2 / V1.3 高级配置和运维接口

| 序号 | 中文名称 | 英文名称 | 后端接口映射 | 完成状态 |
| ---: | --- | --- | --- | --- |
| 179 | 创建过滤文件夹 | `create_filter_folder` | 待确认 | 待实现 |
| 180 | 查询过滤文件夹 | `list_filter_folders` | 待确认 | 待实现 |
| 181 | 编辑过滤文件夹 | `update_filter_folder` | 待确认 | 待实现 |
| 182 | 删除过滤文件夹 | `delete_filter_folder` | 待确认 | 待实现 |
| 183 | 创建过滤器 | `create_filter` | 待确认 | 待实现 |
| 184 | 查询过滤器 | `list_filters` | 待确认 | 待实现 |
| 185 | 查询过滤器详情 | `get_filter` | 待确认 | 待实现 |
| 186 | 编辑过滤器 | `update_filter` | 待确认 | 待实现 |
| 187 | 删除过滤器 | `delete_filter` | 待确认 | 待实现 |
| 188 | 创建过滤项 | `create_filter_item` | 待确认 | 待实现 |
| 189 | 查询过滤项 | `list_filter_items` | 待确认 | 待实现 |
| 190 | 编辑过滤项 | `update_filter_item` | 待确认 | 待实现 |
| 191 | 删除过滤项 | `delete_filter_item` | 待确认 | 待实现 |
| 192 | 查询首页配置 | `get_homepage_config` | 待确认 | 待实现 |
| 193 | 修改首页配置 | `update_homepage_config` | 待确认 | 待实现 |
| 194 | 预览首页配置 | `preview_homepage_config` | 待确认 | 待实现 |
| 195 | 重置首页配置 | `reset_homepage_config` | 待确认 | 待实现 |
| 196 | 查询睿司面板配置 | `get_ruisi_panel_config` | 待确认 | 待实现 |
| 197 | 修改睿司面板配置 | `update_ruisi_panel_config` | 待确认 | 待实现 |
| 198 | 测试睿司面板配置 | `test_ruisi_panel_config` | 待确认 | 待实现 |
| 199 | 启用睿司面板配置 | `enable_ruisi_panel_config` | 待确认 | 待实现 |
| 200 | 停用睿司面板配置 | `disable_ruisi_panel_config` | 待确认 | 待实现 |
| 201 | 创建数据库服务 | `create_database_service` | 待确认 | 待实现 |
| 202 | 查询数据库服务 | `list_database_services` | 待确认 | 待实现 |
| 203 | 查询数据库服务详情 | `get_database_service` | 待确认 | 待实现 |
| 204 | 编辑数据库服务 | `update_database_service` | 待确认 | 待实现 |
| 205 | 删除数据库服务 | `delete_database_service` | 待确认 | 待实现 |
| 206 | 测试数据库连接 | `test_database_service` | 待确认 | 待实现 |
| 207 | 创建数据表文件夹 | `create_database_table_folder` | 待确认 | 待实现 |
| 208 | 查询数据表文件夹 | `list_database_table_folders` | 待确认 | 待实现 |
| 209 | 创建数据表链接 | `create_database_table_link` | 待确认 | 待实现 |
| 210 | 查询数据表链接 | `list_database_table_links` | 待确认 | 待实现 |
| 211 | 查询数据表链接详情 | `get_database_table_link` | 待确认 | 待实现 |
| 212 | 编辑数据表链接 | `update_database_table_link` | 待确认 | 待实现 |
| 213 | 删除数据表链接 | `delete_database_table_link` | 待确认 | 待实现 |
| 214 | 预览数据表数据 | `preview_database_table_data` | 待确认 | 待实现 |
| 215 | 创建接口服务 | `create_api_service` | 待确认 | 待实现 |
| 216 | 查询接口服务 | `list_api_services` | 待确认 | 待实现 |
| 217 | 查询接口服务详情 | `get_api_service` | 待确认 | 待实现 |
| 218 | 编辑接口服务 | `update_api_service` | 待确认 | 待实现 |
| 219 | 删除接口服务 | `delete_api_service` | 待确认 | 待实现 |
| 220 | 测试接口服务 | `test_api_service` | 待确认 | 待实现 |
| 221 | 创建数据接收接口文件夹 | `create_receive_api_folder` | 待确认 | 待实现 |
| 222 | 查询数据接收接口文件夹 | `list_receive_api_folders` | 待确认 | 待实现 |
| 223 | 创建数据接收接口 | `create_receive_api` | 待确认 | 待实现 |
| 224 | 查询数据接收接口 | `list_receive_apis` | 待确认 | 待实现 |
| 225 | 查询数据接收接口详情 | `get_receive_api` | 待确认 | 待实现 |
| 226 | 编辑数据接收接口 | `update_receive_api` | 待确认 | 待实现 |
| 227 | 删除数据接收接口 | `delete_receive_api` | 待确认 | 待实现 |
| 228 | 测试数据接收接口 | `test_receive_api` | 待确认 | 待实现 |
| 229 | 创建网关服务器 | `create_gateway_server` | 待确认 | 待实现 |
| 230 | 查询网关服务器 | `list_gateway_servers` | 待确认 | 待实现 |
| 231 | 查询网关服务器详情 | `get_gateway_server` | 待确认 | 待实现 |
| 232 | 编辑网关服务器 | `update_gateway_server` | 待确认 | 待实现 |
| 233 | 删除网关服务器 | `delete_gateway_server` | 待确认 | 待实现 |
| 234 | 测试网关服务器 | `test_gateway_server` | 待确认 | 待实现 |
| 235 | 创建网关订阅接口文件夹 | `create_gateway_subscribe_folder` | 待确认 | 待实现 |
| 236 | 创建网关订阅接口 | `create_gateway_subscribe_api` | 待确认 | 待实现 |
| 237 | 查询网关订阅接口 | `list_gateway_subscribe_apis` | 待确认 | 待实现 |
| 238 | 编辑网关订阅接口 | `update_gateway_subscribe_api` | 待确认 | 待实现 |
| 239 | 删除网关订阅接口 | `delete_gateway_subscribe_api` | 待确认 | 待实现 |
| 240 | 创建网关发布接口文件夹 | `create_gateway_publish_folder` | 待确认 | 待实现 |
| 241 | 创建网关发布接口 | `create_gateway_publish_api` | 待确认 | 待实现 |
| 242 | 查询网关发布接口 | `list_gateway_publish_apis` | 待确认 | 待实现 |
| 243 | 编辑网关发布接口 | `update_gateway_publish_api` | 待确认 | 待实现 |
| 244 | 删除网关发布接口 | `delete_gateway_publish_api` | 待确认 | 待实现 |
| 245 | 创建字典文件夹 | `create_dictionary_folder` | 待确认 | 待实现 |
| 246 | 查询字典文件夹 | `list_dictionary_folders` | 待确认 | 待实现 |
| 247 | 编辑字典文件夹 | `update_dictionary_folder` | 待确认 | 待实现 |
| 248 | 删除字典文件夹 | `delete_dictionary_folder` | 待确认 | 待实现 |
| 249 | 创建字典数据 | `create_dictionary_data` | 待确认 | 待实现 |
| 250 | 查询字典数据 | `list_dictionary_data` | 待确认 | 待实现 |
| 251 | 查询字典数据详情 | `get_dictionary_data` | 待确认 | 待实现 |
| 252 | 编辑字典数据 | `update_dictionary_data` | 待确认 | 待实现 |
| 253 | 删除字典数据 | `delete_dictionary_data` | 待确认 | 待实现 |
| 254 | 导入字典数据 | `import_dictionary_data` | 待确认 | 待实现 |
| 255 | 导出字典数据 | `export_dictionary_data` | 待确认 | 待实现 |
| 256 | 查询标准孪生体同步项 | `list_standard_twin_sync_items` | 待确认 | 待实现 |
| 257 | 查询标准孪生体同步详情 | `get_standard_twin_sync_item` | 待确认 | 待实现 |
| 258 | 执行标准孪生体同步 | `run_standard_twin_sync` | 待确认 | 待实现 |
| 259 | 查询数据对象同步项 | `list_data_object_sync_items` | 待确认 | 待实现 |
| 260 | 查询数据对象同步详情 | `get_data_object_sync_item` | 待确认 | 待实现 |
| 261 | 执行数据对象同步 | `run_data_object_sync` | 待确认 | 待实现 |
| 262 | 查询同步任务状态 | `get_sync_task_status` | 待确认 | 待实现 |
| 263 | 查询同步任务日志 | `get_sync_task_log` | 待确认 | 待实现 |
| 264 | 查询用户列表 | `list_users` | 待确认 | 待实现 |
| 265 | 查询用户详情 | `get_user` | 待确认 | 待实现 |
| 266 | 编辑用户信息 | `update_user` | 待确认 | 待实现 |
| 267 | 启用用户 | `enable_user` | 待确认 | 待实现 |
| 268 | 停用用户 | `disable_user` | 待确认 | 待实现 |
| 269 | 重置用户密码 | `reset_user_password` | 待确认 | 待实现 |
| 270 | 修改当前用户密码 | `change_current_user_password` | 待确认 | 待实现 |
| 271 | 查询场景显示配置 | `get_scene_display_config` | 待确认 | 待实现 |
| 272 | 修改场景显示配置 | `update_scene_display_config` | 待确认 | 待实现 |
| 273 | 创建导览 | `create_scene_tour` | 待确认 | 待实现 |
| 274 | 查询导览 | `list_scene_tours` | 待确认 | 待实现 |
| 275 | 查询导览详情 | `get_scene_tour` | 待确认 | 待实现 |
| 276 | 编辑导览 | `update_scene_tour` | 待确认 | 待实现 |
| 277 | 删除导览 | `delete_scene_tour` | 待确认 | 待实现 |
| 278 | 创建导览步骤 | `create_scene_tour_step` | 待确认 | 待实现 |
| 279 | 查询导览步骤 | `list_scene_tour_steps` | 待确认 | 待实现 |
| 280 | 编辑导览步骤 | `update_scene_tour_step` | 待确认 | 待实现 |
| 281 | 删除导览步骤 | `delete_scene_tour_step` | 待确认 | 待实现 |
| 282 | 调整导览步骤顺序 | `move_scene_tour_step` | 待确认 | 待实现 |
| 283 | 查询场景工具配置 | `get_scene_tool_config` | 待确认 | 待实现 |
| 284 | 修改场景工具配置 | `update_scene_tool_config` | 待确认 | 待实现 |
| 285 | 查询资产库文件夹 | `list_asset_library_folders` | 待确认 | 待实现 |
| 286 | 查询模型资产 | `list_model_assets` | 待确认 | 待实现 |
| 287 | 查询图片资产 | `list_image_assets` | 待确认 | 待实现 |
| 288 | 查询视频资产 | `list_video_assets` | 待确认 | 待实现 |
| 289 | 查询视频服务资产 | `list_video_service_assets` | 待确认 | 待实现 |
| 290 | 查询社区资产 | `list_community_assets` | 待确认 | 待实现 |
| 291 | 查询粒子资源 | `list_particle_assets` | 待确认 | 待实现 |
| 292 | 查询材质资源 | `list_material_assets` | 待确认 | 待实现 |
| 293 | 查询资产详情 | `get_asset_detail` | 待确认 | 待实现 |
| 294 | 预览资产 | `preview_asset` | 待确认 | 待实现 |

## 当前实现统计

| 分类 | 数量 | 说明 |
| --- | ---: | --- |
| 已实现 | 70 | 认证上下文、场景、层级、POI、路线、区域、租户/场景类别、场景实例台账数据基础操作已完成。 |
| 待实现 | 221 | 已有设计或明确对象，但代码尚未实现。 |
| 待确认 | 3 | 主要是无统一后端接口或 DTO 尚未校准的绑定/取消类能力。 |

## 近期建议

1. 下一步优先补 `bind_twin_instance_resource` / `unbind_twin_instance_resource`，把实例和模型、视频、跳转资源串起来。
2. 再补实例时序和事件数据接口：`upsert/list/delete_twin_instance_timeseries_data`、`upsert/list/delete_twin_instance_event_data`。
3. 之后补模型/设置类配置工具，完善类别展示、可见性、标签和查看面板。