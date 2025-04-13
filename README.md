# Lambda FrameSnap Retry

## 📝 Descrição
O Lambda FrameSnap Retry é uma função AWS Lambda desenvolvida em .NET 8.0 que monitora uma fila DLQ (Dead Letter Queue) do SQS e envia notificações via SNS quando mensagens são encontradas. Este Lambda faz parte do projeto FrameSnap e é responsável por monitorar e notificar falhas no processamento de mensagens.

## 🚀 Funcionalidades
- Monitoramento contínuo da DLQ
- Processamento de até 10 mensagens por invocação
- Notificação via SNS para cada mensagem encontrada na DLQ
- Tratamento de erros com notificação de falhas
- Logging detalhado das operações

## 🛠️ Tecnologias Utilizadas
- .NET 8.0
- AWS Lambda
- Amazon SQS (Simple Queue Service)
- Amazon SNS (Simple Notification Service)
- xUnit para testes
- Moq para mocking em testes

## 📦 Dependências Principais
```xml
<PackageReference Include="Amazon.Lambda.Core" Version="2.2.0" />
<PackageReference Include="Amazon.Lambda.SQSEvents" Version="2.2.0" />
<PackageReference Include="Amazon.Lambda.Serialization.SystemTextJson" Version="2.4.0" />
<PackageReference Include="AWSSDK.SimpleNotificationService" Version="3.7.300.2" />
<PackageReference Include="AWSSDK.SQS" Version="3.7.300.2" />
```

## 📊 Formato das Mensagens

### Notificação de Mensagem na DLQ
```json
{
    "MessageId": "string",
    "Body": "string",
    "Timestamp": "datetime",
    "Error": "Mensagem encontrada na DLQ",
    "QueueUrl": "string"
}
```

### Notificação de Erro
```json
{
    "Error": "Erro ao processar DLQ",
    "Message": "string",
    "StackTrace": "string",
    "Timestamp": "datetime"
}
```

## 🧪 Testes
O projeto inclui testes unitários abrangentes utilizando xUnit e Moq. Para executar os testes:

```bash
dotnet test
```

## 📈 Monitoramento
- Logs são enviados para o CloudWatch Logs
- Métricas padrão do Lambda são disponibilizadas
- Notificações de erro são enviadas via SNS

## 🔒 IAM Permissions Necessárias
```json
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Effect": "Allow",
            "Action": [
                "sqs:ReceiveMessage",
                "sns:Publish"
            ],
            "Resource": [
                "arn:aws:sqs:us-east-1:*:*",
                "arn:aws:sns:us-east-1:*:*"
            ]
        }
    ]
}
```

## 🏗️ Build e Deploy
Para fazer o build do projeto:
```bash
dotnet build
```

Para publicar o projeto:
```bash
dotnet lambda deploy-function
```