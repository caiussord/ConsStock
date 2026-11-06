# API v1 — Fase 3

Base local: `http://localhost:8080`. Em Development, OpenAPI: `/openapi/v1.json`; interface: `/swagger`.

## Rotas

| Método | Rota | Descrição | Acesso |
|---|---|---|---|
| GET | `/health` | Processo ativo | Público |
| GET | `/health/ready` | PostgreSQL acessível | Público |
| POST | `/api/v1/auth/login` | Obter JWT | Público; até 10 tentativas/minuto por IP |
| GET | `/api/v1/auth/me` | Identidade e papéis do token | Autenticado |
| POST | `/api/v1/auth/users` | Criar usuário Admin ou Operator | Admin |
| GET | `/api/v1/categories` | Categorias; `page`, `pageSize`, `active`, `search` | Autenticado |
| GET | `/api/v1/categories/{id}` | Detalhar categoria | Autenticado |
| POST | `/api/v1/categories` | Criar categoria | Admin |
| GET | `/api/v1/products` | Produtos; `page`, `pageSize`, `categoryId`, `active`, `search`, `sort=name\|sku\|createdAt` | Autenticado |
| GET | `/api/v1/products/{id}` | Produto com saldo | Autenticado |
| POST | `/api/v1/products` | Criar produto e saldo inicial zero | Admin |
| PUT | `/api/v1/products/{id}` | Atualizar produto | Admin |
| GET | `/api/v1/stock` | Saldos; `page`, `pageSize`, `categoryId`, `active` | Autenticado |
| GET | `/api/v1/stock/low` | Produtos ativos com saldo abaixo do mínimo | Autenticado |
| GET | `/api/v1/stock/movements` | Histórico; `page`, `pageSize`, `productId`, `type` | Autenticado |
| POST | `/api/v1/stock/movements` | Entrada, saída ou ajuste | Admin ou Operator |

Listas retornam `{ "items": [...], "page": 1, "pageSize": 20, "total": 0 }`. `page` começa em 1 e `pageSize` vai de 1 a 100. Erros de validação usam `ValidationProblem`; conflitos e outros erros tratados usam `ProblemDetails`.

## Login e autorização

O usuário Admin inicial é criado pelas variáveis `Bootstrap__AdminEmail` e `Bootstrap__AdminPassword` em um banco novo. Nenhuma senha é publicada no repositório.

```http
POST /api/v1/auth/login
Content-Type: application/json

{"email":"<EMAIL_ADMIN>","password":"<SENHA_ADMIN>"}
```

A resposta contém `accessToken`, `tokenType` e `expiresAt`:

```json
{"accessToken":"<JWT>","tokenType":"Bearer","expiresAt":"2026-11-06T18:15:00Z"}
```

Use o token nas rotas protegidas:

```http
Authorization: Bearer <ACCESS_TOKEN>
```

O Admin pode criar um Operator:

```http
POST /api/v1/auth/users
Authorization: Bearer <ACCESS_TOKEN_ADMIN>
Content-Type: application/json

{"email":"<EMAIL_OPERATOR>","password":"<SENHA_FORTE_OPERATOR>","role":"Operator"}
```

Senhas têm no mínimo 12 caracteres com letra maiúscula, minúscula, dígito e símbolo. Após 5 senhas incorretas, a conta é bloqueada por 15 minutos. Tokens expiram após 15 minutos e validam assinatura, emissor e audiência. O login responde genericamente em caso de credenciais inválidas.

## Exemplo de fluxo de estoque

Crie uma categoria:

```http
POST /api/v1/categories
Authorization: Bearer <ACCESS_TOKEN_ADMIN>
Content-Type: application/json

{"name":"Informática","description":"Equipamentos e acessórios"}
```

Use o `id` retornado ao criar um produto:

```http
POST /api/v1/products
Authorization: Bearer <ACCESS_TOKEN_ADMIN>
Content-Type: application/json

{"sku":"MOUSE-001","name":"Mouse USB","description":"Modelo de demonstração","categoryId":"<UUID_DA_CATEGORIA>","referencePrice":49.90,"minimumStock":5,"isActive":true}
```

Registre uma entrada:

```http
POST /api/v1/stock/movements
Authorization: Bearer <ACCESS_TOKEN_ADMIN_OU_OPERATOR>
Content-Type: application/json

{"productId":"<UUID_DO_PRODUTO>","type":"Entry","quantity":10,"reason":"Saldo inicial de demonstração"}
```

Para saída, use `"type":"Exit"` e quantidade positiva até o saldo disponível. Para ajuste, use `"type":"Adjustment"` e `quantity` como **saldo final desejado** (pode ser zero). Cada resposta de movimentação inclui `previousBalance` e `newBalance`.

Exemplo de resposta de entrada (`201`):

```json
{"id":"<UUID_MOVIMENTACAO>","productId":"<UUID_DO_PRODUTO>","type":"Entry","quantity":10,"previousBalance":0,"newBalance":10,"reason":"Saldo inicial de demonstração","createdByUserId":"<UUID_USUARIO_AUTENTICADO>","createdAt":"2026-11-06T18:00:00Z"}
```

Sem token ou com token inválido, a API responde `401`; papel insuficiente responde `403`; SKU duplicado, saldo insuficiente ou atualização concorrente respondem `409`. Exemplo de erro:

```json
{"title":"Saldo insuficiente.","status":409,"instance":"/api/v1/stock/movements"}
```

O SKU é normalizado para maiúsculas e é único. Saldo negativo é rejeitado. Saldo e histórico são gravados na mesma transação. Uma mudança concorrente de saldo retorna `409`; o cliente pode consultar o saldo e tentar novamente.

`createdByUserId` na resposta e no histórico é obtido do token validado. Valores com esse nome enviados no corpo da requisição são ignorados. Movimentações anteriores à Fase 3 podem conter identificadores de demonstração.
