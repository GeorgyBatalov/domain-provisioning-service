# Domain Provisioning State Machine

## Overview

DomainProvisioningService использует **state machine** для управления процессом выпуска Let's Encrypt сертификатов.

## Зачем State Machine?

### Проблемы без State Machine:
- ❌ Неявные переходы между состояниями
- ❌ Race conditions при параллельной обработке
- ❌ Сложная отладка ("почему домен застрял?")
- ❌ Нет явного управления retry logic
- ❌ Плохая observability

### Преимущества State Machine:
- ✅ Явные состояния и переходы
- ✅ Централизованное управление логикой
- ✅ История переходов (audit log)
- ✅ Легко отлаживать и тестировать
- ✅ Retry и timeout на уровне engine

## States

### Initial Phase
- **Initial**: Домен только создан, готов к обработке

### DNS Verification Phase
- **DnsVerifying**: Проверяем CNAME запись (retry до max attempts)
- **DnsVerified**: CNAME проверен успешно
- **DnsVerificationFailed**: CNAME не найден после N попыток

### ACME Order Phase
- **AcmeOrdering**: Создаём ACME order в Let's Encrypt
- **AcmeOrderCreated**: Order создан, получили challenge
- **AcmeOrderFailed**: Не удалось создать order

### ACME Challenge Phase
- **AcmeChallengePreparing**: Сохраняем challenge в CertificateStore
- **AcmeChallengePrepared**: Challenge сохранён, доступен через HAProxy Agent
- **AcmeChallengeValidating**: Ждём валидацию от Let's Encrypt (retry)
- **AcmeChallengeValidated**: Let's Encrypt проверил challenge успешно
- **AcmeChallengeFailed**: Валидация не прошла

### Certificate Download Phase
- **CertificateDownloading**: Скачиваем сертификат от Let's Encrypt
- **CertificateDownloaded**: Сертификат получен
- **CertificateDownloadFailed**: Не удалось скачать

### Certificate Deployment Phase
- **CertificateDeploying**: Деплоим сертификат в HAProxy via Agent
- **CertificateDeployed**: Сертификат задеплоен
- **CertificateDeploymentFailed**: Деплой не удался

### Final States
- **Active**: Домен работает с HTTPS ✅
- **Failed**: Критическая ошибка ❌

### Renewal States
- **RenewalDue**: Сертификат скоро истечёт (< 30 дней)
- **Renewing**: Обновляем сертификат (переход в AcmeOrdering)

## State Transitions

```
Initial
  ↓
DnsVerifying (retry до max)
  ↓ success
DnsVerified
  ↓
AcmeOrdering
  ↓ success
AcmeChallengePreparing
  ↓
AcmeChallengePrepared
  ↓
AcmeChallengeValidating (retry до max)
  ↓ success
AcmeChallengeValidated
  ↓
CertificateDownloading
  ↓
CertificateDownloaded
  ↓
CertificateDeploying
  ↓
CertificateDeployed
  ↓
Active ✅
```

## Retry Logic

- **DnsVerifying**: retry до 5 попыток с exponential backoff
- **AcmeChallengeValidating**: retry до 30 попыток (60 секунд)
- Остальные состояния: fail-fast при ошибке

## Timeouts

- **DnsVerifying**: 10 минут
- **AcmeChallengeValidating**: 5 минут
- **CertificateDownloading**: 2 минуты

## Audit Log

Все переходы записываются в `StateTransitionHistory`:
- From/To state
- Timestamp
- Duration в состоянии
- Retry attempt
- Error message (if any)

## Components

### Domain Layer
- `DomainProvisioningState`: enum с состояниями
- `DomainProvisioningContext`: контекст state machine (данные домена)
- `StateTransitionHistory`: audit log

### Application Layer
- `IDomainProvisioningStateMachine`: state machine engine
- `IStateTransitionHandler`: интерфейс для handlers
- Handlers: по одному на каждое состояние

### Infrastructure Layer
- `DomainProvisioningRepository`: persistence для контекстов
- Handlers: конкретные имплементации

### Worker
- `DomainProvisioningStateMachineWorker`: единственный worker
- Загружает все non-terminal контексты
- Для каждого выполняет один transition
- Записывает историю

## Example Flow

```csharp
// 1. Create context
var context = new DomainProvisioningContext
{
    CustomDomainId = guid,
    Domain = "example.com",
    ExpectedCnameValue = "abc123.tunnel.local",
    CurrentState = DomainProvisioningState.Initial
};

// 2. Save context
await _repository.SaveAsync(context);

// 3. Worker picks it up and executes transitions
// Initial → DnsVerifying → DnsVerified → ... → Active
```

## Observability

### Metrics
- Количество доменов в каждом состоянии
- Средняя длительность каждого состояния
- Retry rate по состояниям

### Logs
- Каждый переход логируется
- История переходов в БД

### Alerts
- Домены застряли в состоянии > timeout
- Высокий retry rate
- Много failures
