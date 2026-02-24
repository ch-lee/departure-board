


## 
Sign up at https://realtime.nationalrail.co.uk/OpenLDBWSRegistration/

https://lite.realtime.nationalrail.co.uk/OpenLDBWS/

```yaml
version: '3.2'

services:
  departure-board:
    container_name: departure-board
    image: docker pull ghcr.io/ch-lee/departure-board:latest
    environment:
      TRMNL_WEBHOOK_URL: https://usetrmnl.com/api/custom_plugins/dcb865af-dee4-4705-be2b-b137f5ec1d74
      NETWORK_RAIL_ACCESS_TOKEN: f248f736-0172-479e-a393-80ddfc9e8b28
      SKIP_SEND_TO_TRMNL: true
      Routes__0__From: AAP
      Routes__0__To: MOG
      Routes__1__From: AAP
      Routes__1__To: KGX
```