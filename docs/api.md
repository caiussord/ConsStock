# API v1 — Fase 2

Base local: `http://localhost:8080`. Em Development, OpenAPI: `/openapi/v1.json`; interface: `/swagger`.

## Rotas

| Método | Rota | Descrição |
|---|---|---|
| GET | `/health` | Processo ativo |
| GET | `/health/ready` | PostgreSQL acessível |
| GET | `/api/v1/categories` | Categorias; `page`, `pageSize`, `active`, `search` |
| GET | `/api/v1/categories/{id}` | Detalhar categoria |
| POST | `/api/v1/categories` | Criar categoria |
| GET | `/api/v1/products` | Produtos; `page`, `pageSize`, `categoryId`, `active`, `search`, `sort=name\|sku\|createdAt` |
| GET | `/api/v1/products/{id}` | Produto com saldo |
| POST | `/api/v1/products` | Criar produto e saldo inicial zero |
| PUT | `/api/v1/products/{id}` | Atualizar produto |
| GET | `/api/v1/stock` | Saldos; `page`, `pageSize`, `categoryId`, `active` |
| GET | `/api/v1/stock/low` | Produtos ativos com saldo abaixo do mínimo |
| GET | `/api/v1/stock/movements` | Histórico; `page`, `pageSize`, `productId`, `type` |
| POST | `/api/v1/stock/movements` | Entrada, saída ou ajuste |

Listas retornam `{ "items": [...], "page": 1, "pageSize": 20, "total": 0 }`. `page` começa em 1 e `pageSize` vai de 1 a 100. Erros de validação usam `ValidationProblem`; conflitos e outros erros tratados usam `ProblemDetails`.

## Exemplo de fluxo

Crie uma categoria:

```http
POST /api/v1/categories
Content-Type: application/json

{"name":"Informática","description":"Equipamentos e acessórios"}
```

Use o `id` retornado ao criar um produto:

```http
POST /api/v1/products
Content-Type: application/json

{"sku":"MOUSE-001","name":"Mouse USB","description":"Modelo de demonstração","categoryId":"<UUID_DA_CATEGORIA>","referencePrice":49.90,"minimumStock":5,"isActive":true}
```

Registre uma entrada:

```http
POST /api/v1/stock/movements
Content-Type: application/json

{"productId":"<UUID_DO_PRODUTO>","type":"Entry","quantity":10,"reason":"Saldo inicial de demonstração","createdByUserId":"demo-operator"}
```

Para saída, use `"type":"Exit"` e quantidade positiva até o saldo disponível. Para ajuste, use `"type":"Adjustment"` e `quantity` como **saldo final desejado** (pode ser zero). Cada resposta de movimentação inclui `previousBalance` e `newBalance`.

O SKU é normalizado para maiúsculas e é único. Saldo negativo é rejeitado. Saldo e histórico são gravados na mesma transação. Uma mudança concorrente de saldo retorna `409`; o cliente pode consultar o saldo e tentar novamente.

## Segurança nesta fase

A Fase 2 ainda não tem autenticação. `createdByUserId` é um identificador de demonstração fornecido pelo cliente; não deve ser tratado como auditoria de identidade confiável. JWT e papéis serão implementados na Fase 3.
