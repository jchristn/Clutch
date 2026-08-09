// Translation catalogs for the Clutch dashboard.
//
// English (`en`) is the source of truth. German (`de`) and Japanese (`ja`) are
// layered as overrides on top of English (deep-merged), so any missing key falls
// back to English. The RTL pseudo-locale (`zz`) is generated programmatically
// from English to guarantee full coverage for bidi + expansion testing.

const en = {
  common: {
    appName: 'Clutch',
    tagline: 'Distributed lock management',
    language: { label: 'Language' },
    theme: {
      light: 'Light',
      dark: 'Dark',
      switchToLight: 'Switch to light theme',
      switchToDark: 'Switch to dark theme'
    },
    actions: {
      copy: 'Copy',
      copied: 'Copied',
      close: 'Close',
      cancel: 'Cancel',
      confirm: 'Confirm',
      save: 'Save',
      create: 'Create',
      delete: 'Delete',
      edit: 'Edit',
      view: 'View',
      viewJson: 'View JSON',
      viewDetails: 'View details',
      refresh: 'Refresh',
      retry: 'Retry',
      apply: 'Apply',
      clear: 'Clear',
      add: 'Add',
      release: 'Force release',
      execute: 'Execute',
      logout: 'Log out',
      back: 'Back',
      openGithub: 'Open the repository on GitHub'
    },
    boolean: { yes: 'Yes', no: 'No' },
    generic: {
      loading: 'Loading…',
      error: 'Something went wrong',
      none: 'None',
      all: 'All',
      any: 'Any',
      na: '—',
      unknown: 'Unknown',
      actions: 'Actions',
      search: 'Search',
      saving: 'Saving…',
      required: 'Required',
      optional: 'Optional',
      active: 'Active',
      inactive: 'Inactive'
    },
    relativeTime: { now: 'just now' },
    units: { msShort: 'ms', sShort: 's', mShort: 'min' },
    errors: {
      loadFailed: 'Failed to load data.',
      network: 'Could not reach the server. Check the server URL and try again.'
    }
  },
  nav: {
    groups: {
      overview: 'Overview',
      locks: 'Locks',
      administration: 'Administration',
      observability: 'Observability',
      system: 'System'
    },
    items: {
      home: { label: 'Home', tip: 'System KPIs and activity' },
      locks: { label: 'Locks', tip: 'Live lock holders' },
      lockActivity: { label: 'Lock Activity', tip: 'Lock audit history and chart' },
      tenants: { label: 'Tenants', tip: 'Manage tenants' },
      users: { label: 'Users', tip: 'Manage tenant users' },
      credentials: { label: 'Credentials', tip: 'Application keys' },
      requests: { label: 'Request History', tip: 'Inspect API traffic' },
      explorer: { label: 'API Explorer', tip: 'Execute API calls' },
      settings: { label: 'Server Info', tip: 'Endpoint and configuration' },
      settingsAdmin: { label: 'Server Settings', tip: 'Edit server configuration' }
    },
    version: 'Version {{version}}',
    collapseSidebar: 'Collapse sidebar',
    expandSidebar: 'Expand sidebar'
  },
  login: {
    title: 'Clutch',
    subtitle: 'Sign in to the operator console',
    tabs: { appKey: 'Application key', email: 'Email' },
    serverUrl: 'Server URL',
    accessKey: 'Access key',
    tenantId: 'Tenant ID',
    email: 'Email',
    password: 'Password',
    serverUrlPlaceholder: 'http://localhost:8090',
    accessKeyPlaceholder: 'access key',
    tenantIdPlaceholder: 'ten_…',
    emailPlaceholder: 'you@example.com',
    passwordPlaceholder: 'password',
    submit: 'Connect',
    connecting: 'Connecting…',
    showPassword: 'Show password',
    hidePassword: 'Hide password',
    errorGeneric: 'Failed to connect. Check your settings and try again.',
    footerHint: 'Enter your server URL and application key to connect.'
  },
  topbar: {
    server: 'Server',
    principal: 'Principal',
    copyServer: 'Copy server URL',
    health: { healthy: 'Healthy', unhealthy: 'Unhealthy', unknown: 'Unknown' }
  },
  roles: {
    systemAdmin: 'System admin',
    tenantAdmin: 'Tenant admin',
    user: 'User',
    credential: 'Application key'
  },
  components: {
    table: {
      records_one: 'record',
      records_other: 'records',
      showing: 'Showing {{from}}–{{to}}',
      pageOf: 'Page {{page}} of {{total}}',
      first: 'First page',
      prev: 'Previous page',
      next: 'Next page',
      last: 'Last page',
      jump: 'Jump to page',
      pageSize: 'Per page',
      refresh: 'Refresh',
      empty: 'No records to display.',
      loading: 'Loading records…',
      selected: '{{count}} selected',
      clearSelection: 'Clear selection',
      bulkDelete: 'Delete selected'
    },
    modal: { close: 'Close' },
    confirm: {
      title: 'Please confirm',
      message: 'Are you sure you want to continue?'
    },
    autoRefresh: {
      label: 'Auto-refresh',
      none: 'Off',
      seconds_one: 'Every {{count}} second',
      seconds_other: 'Every {{count}} seconds'
    },
    chart: {
      success: 'Success',
      failure: 'Failed',
      total: 'Total',
      avgDuration: 'Avg duration',
      noData: 'No activity in this range.',
      ranges: { hour: 'Last hour', day: 'Last day', week: 'Last week', month: 'Last month' },
      modes: { line: 'Line', bar: 'Stacked bar' },
      legend: 'Legend',
      bucketRange: '{{start}} – {{end}}'
    },
    requestDetails: {
      title: 'Request details',
      metadata: 'Metadata',
      requestHeaders: 'Request headers',
      requestBody: 'Request body',
      responseHeaders: 'Response headers',
      responseBody: 'Response body',
      rawJson: 'Raw JSON',
      truncated: 'Truncated ({{bytes}} bytes original)',
      empty: 'No content.',
      fields: {
        id: 'Request ID',
        method: 'Method',
        path: 'Path',
        url: 'URL',
        status: 'Status',
        duration: 'Duration',
        createdUtc: 'Created',
        completedUtc: 'Completed',
        sourceIp: 'Source IP',
        principal: 'Principal',
        tenant: 'Tenant',
        user: 'User'
      }
    },
    filters: {
      title: 'Filters',
      method: 'Method',
      status: 'Status code',
      path: 'Path contains',
      from: 'From',
      to: 'To',
      tenant: 'Tenant',
      name: 'Lock name',
      mode: 'Lock mode',
      eventType: 'Event type',
      apply: 'Apply',
      clear: 'Clear',
      statusPlaceholder: 'e.g. 404',
      pathPlaceholder: 'e.g. /v1.0/api/tenants',
      namePlaceholder: 'e.g. orders'
    },
    copy: { copy: 'Copy', copied: 'Copied' },
    states: {
      errorTitle: 'Could not load this view',
      emptyTitle: 'Nothing here yet'
    }
  },
  views: {
    home: {
      title: 'Home',
      subtitle: 'Current state of the Clutch cluster and recent activity.',
      kpis: {
        activeLocks: 'Active lock holders',
        writeHolders: 'Write holders',
        tenants: 'Tenants',
        credentials: 'Application keys'
      },
      lockActivity: 'Lock activity',
      requestActivity: 'Request activity',
      recentEvents: 'Recent denials and expiries',
      recentEventsEmpty: 'No denials or expiries in the selected range.',
      cta: {
        title: 'Common tasks',
        viewLocks: 'View live locks',
        requestHistory: 'Inspect request history',
        apiExplorer: 'Open API Explorer',
        addCredential: 'Create application key'
      },
      eventColumns: { time: 'Time', lockKey: 'Lock key', mode: 'Mode', event: 'Event', reason: 'Reason' }
    },
    locks: {
      title: 'Locks',
      subtitle: 'Live lock holders. Filter by name and mode; force-release as an administrator.',
      columns: {
        lockKey: 'Lock key',
        mode: 'Mode',
        credential: 'Credential',
        session: 'Session',
        node: 'Node',
        fencingToken: 'Fencing token',
        acquiredUtc: 'Acquired',
        leaseExpiresUtc: 'Lease expires'
      },
      forceRelease: 'Force release',
      confirmReleaseTitle: 'Force-release lock',
      confirmReleaseMessage: 'Release all holders of lock "{{key}}"? Holding clients will lose the lock immediately.',
      released: 'Released lock "{{key}}".',
      emptyTitle: 'No active lock holders',
      emptyBody: 'No holders match the current filters. Locks appear here while clients hold them.'
    },
    lockActivity: {
      title: 'Lock Activity',
      subtitle: 'Lock lifecycle events over time, with an audit trail.',
      auditColumns: {
        time: 'Time',
        lockKey: 'Lock key',
        mode: 'Mode',
        event: 'Event',
        credential: 'Credential',
        session: 'Session',
        node: 'Node',
        fencingToken: 'Fencing token',
        reason: 'Reason'
      },
      emptyTitle: 'No lock audit entries',
      emptyBody: 'No lock lifecycle events match the current filters.'
    },
    tenants: {
      title: 'Tenants',
      subtitle: 'Isolated lock namespaces, each with users and application keys.',
      columns: {
        name: 'Name',
        id: 'ID',
        retention: 'Audit retention',
        defaultLease: 'Default lease',
        maxLease: 'Max lease',
        active: 'Active',
        created: 'Created'
      },
      add: 'Add tenant',
      create: 'Create tenant',
      edit: 'Edit tenant',
      form: {
        name: 'Name',
        retention: 'Lock audit retention (days)',
        defaultLease: 'Default lease (ms)',
        maxLease: 'Max lease (ms)'
      },
      confirmDeleteTitle: 'Delete tenant',
      confirmDeleteMessage: 'Delete tenant "{{name}}"? This cannot be undone.',
      days: '{{count}} days',
      emptyTitle: 'No tenants',
      emptyBody: 'Create a tenant to isolate a set of locks, users, and application keys.'
    },
    users: {
      title: 'Users',
      subtitle: 'Tenant users that authenticate with email and password.',
      columns: {
        email: 'Email',
        name: 'Name',
        id: 'ID',
        role: 'Role',
        active: 'Active',
        created: 'Created'
      },
      add: 'Add user',
      create: 'Create user',
      edit: 'Edit user',
      form: {
        email: 'Email',
        password: 'Password',
        firstName: 'First name',
        lastName: 'Last name',
        isSystemAdmin: 'System administrator',
        isTenantAdmin: 'Tenant administrator',
        active: 'Active'
      },
      confirmDeleteTitle: 'Delete user',
      confirmDeleteMessage: 'Delete user "{{email}}"? This cannot be undone.',
      emptyTitle: 'No users',
      emptyBody: 'Add a user so a person can sign in to this tenant.',
      selectTenant: 'Select a tenant to view its users.'
    },
    credentials: {
      title: 'Credentials',
      subtitle: 'Application keys used by clients and automation to authenticate.',
      columns: {
        name: 'Name',
        id: 'ID',
        accessKey: 'Access key',
        expires: 'Expires',
        lastUsed: 'Last used',
        active: 'Active',
        created: 'Created'
      },
      add: 'Add application key',
      create: 'Create application key',
      form: { name: 'Name', userId: 'User ID (optional)', expiresUtc: 'Expires (optional)' },
      confirmDeleteTitle: 'Delete application key',
      confirmDeleteMessage: 'Delete application key "{{name}}"? Clients using it will fail to authenticate.',
      secretTitle: 'Application key created',
      accessKeyLabel: 'Access key',
      secretDone: 'Done',
      emptyTitle: 'No application keys',
      emptyBody: 'Create an application key so a client or script can connect to Clutch.',
      selectTenant: 'Select a tenant to view its application keys.'
    },
    requests: {
      title: 'Request History',
      subtitle: 'Inspect captured API traffic, drill into any request, and manage retention.',
      kpis: {
        total: 'Retained requests',
        success: 'Successful',
        failure: 'Failures',
        avgDuration: 'Avg duration'
      },
      columns: {
        time: 'Time',
        method: 'Method',
        path: 'Path',
        status: 'Status',
        duration: 'Duration',
        principal: 'Principal'
      },
      bulkDeleteTitle: 'Delete request history',
      bulkDeleteMessage: 'Delete all {{count}} requests matching the current filters?',
      bulkDeleteAll: 'Delete all matching',
      deleteOneTitle: 'Delete request',
      deleteOneMessage: 'Delete request {{id}}?',
      deleted: 'Deleted request.',
      emptyTitle: 'No request history',
      emptyBodyNoTraffic: 'No API traffic has been retained yet.',
      emptyBodyNoMatch: 'No requests match the current filters.'
    },
    explorer: {
      title: 'API Explorer',
      subtitle: 'Execute live API calls against this server, driven by its OpenAPI document.',
      searchOperations: 'Search operations',
      operations: 'Operations',
      resources: 'Resources',
      selected: 'Selected',
      source: 'Source',
      sourceLive: 'Live OpenAPI',
      pathParams: 'Path parameters',
      queryParams: 'Query parameters',
      headers: 'Headers',
      body: 'Request body',
      resolvedUrl: 'Resolved URL',
      response: 'Response',
      responseHeaders: 'Response headers',
      responseBody: 'Response body',
      execute: 'Execute',
      executing: 'Executing…',
      snippets: 'Code snippets',
      history: 'Recent requests',
      historyEmpty: 'No recent requests.',
      loadHistory: 'Load',
      deleteHistory: 'Delete',
      clearHistory: 'Clear history',
      status: 'Status',
      duration: 'Duration',
      size: 'Size',
      confirmTitle: 'Execute destructive operation',
      confirmMessage: 'This is a {{method}} request and may modify or delete data. Continue?',
      noSpecTitle: 'OpenAPI document unavailable',
      noSpecBody: 'This server did not return a usable /openapi.json document, so the explorer cannot render operations.',
      selectOperation: 'Select an operation to build a request.',
      selectOperationOption: 'Choose an operation…',
      addHeader: 'Add header',
      headerName: 'Header',
      headerValue: 'Value',
      copyUrl: 'Copy URL',
      required: 'required'
    },
    settings: {
      title: 'Server Info',
      subtitle: 'Endpoint, version, telemetry, and the authenticated principal.',
      sections: {
        server: 'Server',
        connection: 'Connection',
        telemetry: 'Telemetry',
        principal: 'Authenticated principal'
      },
      fields: {
        product: 'Product',
        version: 'Version',
        node: 'Node',
        database: 'Database',
        webSocketConnections: 'WebSocket connections',
        serverUrl: 'Server URL',
        endpoint: 'Endpoint',
        telemetryEnabled: 'Telemetry enabled',
        prometheusPort: 'Prometheus port',
        prometheusPath: 'Prometheus path',
        authenticated: 'Authenticated',
        tenantId: 'Tenant ID',
        principalName: 'Principal name',
        isAdmin: 'System admin',
        isTenantAdmin: 'Tenant admin'
      },
      emptyTitle: 'Server info unavailable',
      emptyBody: 'Could not load server information from this endpoint.'
    },
    serverSettings: {
      title: 'Server Settings',
      subtitle: 'Edit the on-disk server configuration for this node.',
      restartBanner: 'Changes to these settings require a node restart to take effect.',
      readOnlyNotice: 'You are viewing server settings in read-only mode. Only system administrators can edit them.',
      save: 'Save settings',
      saved: 'Settings saved.',
      restartRequiredToast: 'Settings saved. Restart the node to apply the changes.',
      restart: 'Restart node',
      restartConfirmTitle: 'Restart node',
      restartConfirmMessage: 'Restart this node now? Active connections will be interrupted and the console will disconnect briefly.',
      restarting: 'The node is restarting. Reconnect shortly.',
      restartToast: 'Restart requested. The node is restarting.',
      secretHint: 'Leave as *** to keep the current value.',
      emptyTitle: 'Settings unavailable',
      emptyBody: 'Could not load server settings from this endpoint.',
      sections: {
        node: 'Node',
        rest: 'REST',
        database: 'Database',
        logging: 'Logging',
        auth: 'Authentication',
        lock: 'Lock',
        requestHistory: 'Request History',
        telemetry: 'Telemetry'
      },
      fields: {
        node: {
          nodeId: 'Node ID',
          createdUtc: 'Created'
        },
        rest: {
          hostname: 'Hostname',
          port: 'Port',
          ssl: 'SSL enabled'
        },
        database: {
          type: 'Type',
          host: 'Host',
          port: 'Port',
          databaseName: 'Database name',
          username: 'Username',
          password: 'Password',
          maxPoolSize: 'Max pool size'
        },
        logging: {
          consoleLogging: 'Console logging',
          fileLogging: 'File logging',
          logDirectory: 'Log directory',
          logFilename: 'Log filename',
          minimumSeverity: 'Minimum severity'
        },
        auth: {
          issuer: 'Issuer',
          signingKey: 'Signing key',
          tokenLifetimeMinutes: 'Token lifetime (minutes)',
          adminApiKey: 'Admin API key'
        },
        lock: {
          defaultLeaseMs: 'Default lease (ms)',
          maxLeaseMs: 'Max lease (ms)',
          maxHoldMs: 'Max hold (ms)',
          maxWaitMs: 'Max wait (ms)',
          waiterPollMs: 'Waiter poll (ms)',
          sweepIntervalMs: 'Sweep interval (ms)'
        },
        requestHistory: {
          enabled: 'Enabled',
          maxRequestBodyBytes: 'Max request body (bytes)',
          maxResponseBodyBytes: 'Max response body (bytes)',
          retentionDays: 'Retention (days)'
        },
        telemetry: {
          enabled: 'Enabled',
          serviceName: 'Service name',
          prometheusEnable: 'Prometheus enabled',
          prometheusHostname: 'Prometheus hostname',
          prometheusPort: 'Prometheus port',
          prometheusPath: 'Prometheus path',
          otlpEnable: 'OTLP enabled',
          otlpEndpoint: 'OTLP endpoint',
          metricsIncludeRuntime: 'Include runtime metrics',
          metricsIncludeProcess: 'Include process metrics'
        }
      }
    }
  }
};

// ---------------------------------------------------------------------------
// German overrides (high-visibility surfaces; the rest falls back to English).
// ---------------------------------------------------------------------------
const de = {
  common: {
    tagline: 'Verwaltung verteilter Sperren',
    language: { label: 'Sprache' },
    theme: {
      light: 'Hell',
      dark: 'Dunkel',
      switchToLight: 'Zum hellen Design wechseln',
      switchToDark: 'Zum dunklen Design wechseln'
    },
    actions: {
      copy: 'Kopieren',
      copied: 'Kopiert',
      close: 'Schließen',
      cancel: 'Abbrechen',
      confirm: 'Bestätigen',
      save: 'Speichern',
      create: 'Erstellen',
      delete: 'Löschen',
      edit: 'Bearbeiten',
      view: 'Ansehen',
      viewJson: 'JSON anzeigen',
      viewDetails: 'Details anzeigen',
      refresh: 'Aktualisieren',
      retry: 'Wiederholen',
      apply: 'Anwenden',
      clear: 'Zurücksetzen',
      add: 'Hinzufügen',
      release: 'Freigeben erzwingen',
      execute: 'Ausführen',
      logout: 'Abmelden',
      back: 'Zurück',
      openGithub: 'Repository auf GitHub öffnen'
    },
    boolean: { yes: 'Ja', no: 'Nein' },
    generic: {
      loading: 'Wird geladen…',
      error: 'Ein Fehler ist aufgetreten',
      none: 'Keine',
      all: 'Alle',
      any: 'Beliebig',
      unknown: 'Unbekannt',
      actions: 'Aktionen',
      search: 'Suchen',
      saving: 'Wird gespeichert…',
      required: 'Erforderlich',
      optional: 'Optional',
      active: 'Aktiv',
      inactive: 'Inaktiv'
    },
    relativeTime: { now: 'gerade eben' },
    units: { msShort: 'ms', sShort: 's', mShort: 'Min' },
    errors: {
      loadFailed: 'Daten konnten nicht geladen werden.',
      network: 'Server nicht erreichbar. Prüfen Sie die Server-URL und versuchen Sie es erneut.'
    }
  },
  nav: {
    groups: {
      overview: 'Übersicht',
      locks: 'Sperren',
      administration: 'Verwaltung',
      observability: 'Beobachtbarkeit',
      system: 'System'
    },
    items: {
      home: { label: 'Start', tip: 'Kennzahlen und Aktivität' },
      locks: { label: 'Sperren', tip: 'Aktive Sperrhalter' },
      lockActivity: { label: 'Sperraktivität', tip: 'Sperr-Audit und Diagramm' },
      tenants: { label: 'Mandanten', tip: 'Mandanten verwalten' },
      users: { label: 'Benutzer', tip: 'Benutzer verwalten' },
      credentials: { label: 'Anmeldedaten', tip: 'Anwendungsschlüssel' },
      requests: { label: 'Anfrageverlauf', tip: 'API-Verkehr prüfen' },
      explorer: { label: 'API-Explorer', tip: 'API-Aufrufe ausführen' },
      settings: { label: 'Serverinfo', tip: 'Endpunkt und Konfiguration' }
    },
    version: 'Version {{version}}'
  },
  login: {
    subtitle: 'Bei der Betreiberkonsole anmelden',
    tabs: { appKey: 'Anwendungsschlüssel', email: 'E-Mail' },
    serverUrl: 'Server-URL',
    accessKey: 'Zugriffsschlüssel',
    tenantId: 'Mandanten-ID',
    email: 'E-Mail',
    password: 'Passwort',
    submit: 'Verbinden',
    connecting: 'Verbindung…',
    errorGeneric: 'Verbindung fehlgeschlagen. Prüfen Sie Ihre Angaben.',
    footerHint: 'Geben Sie Server-URL und Anwendungsschlüssel ein.'
  },
  roles: {
    systemAdmin: 'Systemadmin',
    tenantAdmin: 'Mandantenadmin',
    user: 'Benutzer',
    credential: 'Anwendungsschlüssel'
  },
  components: {
    table: {
      records_one: 'Eintrag',
      records_other: 'Einträge',
      pageOf: 'Seite {{page}} von {{total}}',
      pageSize: 'Pro Seite',
      empty: 'Keine Einträge vorhanden.',
      selected: '{{count}} ausgewählt',
      bulkDelete: 'Auswahl löschen'
    },
    autoRefresh: {
      label: 'Auto-Aktualisierung',
      none: 'Aus',
      seconds_one: 'Alle {{count}} Sekunde',
      seconds_other: 'Alle {{count}} Sekunden'
    },
    chart: {
      success: 'Erfolg',
      failure: 'Fehler',
      total: 'Gesamt',
      avgDuration: 'Ø Dauer',
      noData: 'Keine Aktivität in diesem Zeitraum.',
      ranges: { hour: 'Letzte Stunde', day: 'Letzter Tag', week: 'Letzte Woche', month: 'Letzter Monat' },
      modes: { line: 'Linie', bar: 'Gestapelt' }
    }
  },
  views: {
    home: { title: 'Start', subtitle: 'Aktueller Zustand des Clutch-Clusters und jüngste Aktivität.' },
    locks: { title: 'Sperren' },
    lockActivity: { title: 'Sperraktivität' },
    tenants: { title: 'Mandanten' },
    users: { title: 'Benutzer' },
    credentials: { title: 'Anmeldedaten' },
    requests: { title: 'Anfrageverlauf' },
    explorer: { title: 'API-Explorer' },
    settings: { title: 'Serverinfo' }
  }
};

// ---------------------------------------------------------------------------
// Japanese overrides (high-visibility surfaces; the rest falls back to English).
// ---------------------------------------------------------------------------
const ja = {
  common: {
    tagline: '分散ロック管理',
    language: { label: '言語' },
    theme: {
      light: 'ライト',
      dark: 'ダーク',
      switchToLight: 'ライトテーマに切り替え',
      switchToDark: 'ダークテーマに切り替え'
    },
    actions: {
      copy: 'コピー',
      copied: 'コピーしました',
      close: '閉じる',
      cancel: 'キャンセル',
      confirm: '確認',
      save: '保存',
      create: '作成',
      delete: '削除',
      edit: '編集',
      view: '表示',
      viewJson: 'JSON を表示',
      viewDetails: '詳細を表示',
      refresh: '更新',
      retry: '再試行',
      apply: '適用',
      clear: 'クリア',
      add: '追加',
      release: '強制解放',
      execute: '実行',
      logout: 'ログアウト',
      back: '戻る',
      openGithub: 'GitHub でリポジトリを開く'
    },
    boolean: { yes: 'はい', no: 'いいえ' },
    generic: {
      loading: '読み込み中…',
      error: '問題が発生しました',
      none: 'なし',
      all: 'すべて',
      any: '任意',
      unknown: '不明',
      actions: '操作',
      search: '検索',
      saving: '保存中…',
      required: '必須',
      optional: '任意',
      active: '有効',
      inactive: '無効'
    },
    relativeTime: { now: 'たった今' },
    units: { msShort: 'ミリ秒', sShort: '秒', mShort: '分' },
    errors: {
      loadFailed: 'データの読み込みに失敗しました。',
      network: 'サーバーに接続できません。サーバー URL を確認してください。'
    }
  },
  nav: {
    groups: {
      overview: '概要',
      locks: 'ロック',
      administration: '管理',
      observability: '可観測性',
      system: 'システム'
    },
    items: {
      home: { label: 'ホーム', tip: 'KPI とアクティビティ' },
      locks: { label: 'ロック', tip: 'アクティブな保持者' },
      lockActivity: { label: 'ロック活動', tip: 'ロック監査とチャート' },
      tenants: { label: 'テナント', tip: 'テナント管理' },
      users: { label: 'ユーザー', tip: 'ユーザー管理' },
      credentials: { label: '認証情報', tip: 'アプリケーションキー' },
      requests: { label: 'リクエスト履歴', tip: 'API トラフィックを検査' },
      explorer: { label: 'API エクスプローラー', tip: 'API 呼び出しを実行' },
      settings: { label: 'サーバー情報', tip: 'エンドポイントと構成' }
    },
    version: 'バージョン {{version}}'
  },
  login: {
    subtitle: 'オペレーターコンソールにサインイン',
    tabs: { appKey: 'アプリケーションキー', email: 'メール' },
    serverUrl: 'サーバー URL',
    accessKey: 'アクセスキー',
    tenantId: 'テナント ID',
    email: 'メール',
    password: 'パスワード',
    submit: '接続',
    connecting: '接続中…',
    errorGeneric: '接続に失敗しました。設定を確認してください。',
    footerHint: 'サーバー URL とアプリケーションキーを入力してください。'
  },
  roles: {
    systemAdmin: 'システム管理者',
    tenantAdmin: 'テナント管理者',
    user: 'ユーザー',
    credential: 'アプリケーションキー'
  },
  components: {
    table: {
      records_one: '件',
      records_other: '件',
      pageOf: '{{total}} ページ中 {{page}} ページ',
      pageSize: '表示件数',
      empty: '表示するレコードがありません。',
      selected: '{{count}} 件選択中',
      bulkDelete: '選択を削除'
    },
    autoRefresh: {
      label: '自動更新',
      none: 'オフ',
      seconds_one: '{{count}} 秒ごと',
      seconds_other: '{{count}} 秒ごと'
    },
    chart: {
      success: '成功',
      failure: '失敗',
      total: '合計',
      avgDuration: '平均時間',
      noData: 'この範囲にアクティビティはありません。',
      ranges: { hour: '過去 1 時間', day: '過去 1 日', week: '過去 1 週間', month: '過去 1 か月' },
      modes: { line: '折れ線', bar: '積み上げ棒' }
    }
  },
  views: {
    home: { title: 'ホーム', subtitle: 'Clutch クラスターの現在の状態と最近のアクティビティ。' },
    locks: { title: 'ロック' },
    lockActivity: { title: 'ロック活動' },
    tenants: { title: 'テナント' },
    users: { title: 'ユーザー' },
    credentials: { title: '認証情報' },
    requests: { title: 'リクエスト履歴' },
    explorer: { title: 'API エクスプローラー' },
    settings: { title: 'サーバー情報' }
  }
};

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------
function deepMerge(base, override) {
  const out = Array.isArray(base) ? [...base] : { ...base };
  for (const [key, value] of Object.entries(override || {})) {
    if (value && typeof value === 'object' && !Array.isArray(value) && typeof out[key] === 'object') {
      out[key] = deepMerge(out[key], value);
    } else {
      out[key] = value;
    }
  }
  return out;
}

const ACCENT_MAP = {
  a: 'á', e: 'é', i: 'í', o: 'ó', u: 'ú', A: 'Á', E: 'É', I: 'Í', O: 'Ó', U: 'Ú',
  n: 'ñ', c: 'ç', s: 'š', y: 'ý'
};

/**
 * Generate a pseudo-locale string: accent letters, wrap in brackets, and pad to
 * ~35% longer to exercise expansion. Preserves {{interpolation}} placeholders.
 */
function pseudoString(value) {
  const parts = String(value).split(/(\{\{[^}]+\}\})/g);
  let accented = parts
    .map((part) => {
      if (part.startsWith('{{') && part.endsWith('}}')) return part;
      return part.replace(/[a-zA-Z]/g, (ch) => ACCENT_MAP[ch] || ch);
    })
    .join('');
  const padCount = Math.max(1, Math.round(accented.replace(/\{\{[^}]+\}\}/g, '').length * 0.35));
  const pad = '·'.repeat(padCount);
  return `⟦${accented}${pad}⟧`;
}

/** Recursively transform every leaf string of an object into pseudo-locale text. */
function pseudoize(node) {
  if (typeof node === 'string') return pseudoString(node);
  if (Array.isArray(node)) return node.map(pseudoize);
  if (node && typeof node === 'object') {
    const out = {};
    for (const [key, value] of Object.entries(node)) out[key] = pseudoize(value);
    return out;
  }
  return node;
}

const zz = pseudoize(en);

const resources = {
  en: { translation: en },
  de: { translation: deepMerge(en, de) },
  ja: { translation: deepMerge(en, ja) },
  zz: { translation: zz }
};

export default resources;
