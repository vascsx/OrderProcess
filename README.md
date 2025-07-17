# OrderProcess

Sistema de processamento de pedidos com mensageria, persistência em banco de dados e arquitetura desacoplada.

## 🧩 Visão Geral

O projeto **OrderProcess** implementa um fluxo completo de mensageria para processamento de pedidos utilizando:

- **.NET 8**
- **RabbitMQ** (mensageria)
- **SQL Server** (persistência)
- **Docker** (containerização)
- Arquitetura baseada em **MVC** e **Repository Pattern**

O sistema é dividido em duas partes principais:
- **API (Publisher)**: Publica mensagens no RabbitMQ e registra dados no banco SQL.
- **Worker (Consumer)**: Consome mensagens da fila e processa os pedidos.

---

## 🔁 Fluxo de Mensageria

```mermaid
graph TD
    A[Cliente envia requisição para a API] --> B[API publica mensagem no RabbitMQ]
    B --> C[Worker consome mensagem da fila]
    C --> D[Worker processa e grava no banco de dados]
