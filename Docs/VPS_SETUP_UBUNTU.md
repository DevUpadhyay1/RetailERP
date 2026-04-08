# VPS setup (Ubuntu 22.04) - QuickBusiness

Use this runbook to deploy RetailERP publicly on a VPS with Docker + Nginx + HTTPS.

## 1) Create VPS

- OS: Ubuntu 22.04 LTS
- Size: 2 vCPU, 4 GB RAM, 80 GB SSD
- Open inbound ports in cloud firewall: 22, 80, 443

## 2) Point DNS to VPS IP

In GoDaddy for quickbusiness.co.in:

- A: @ -> <VPS_PUBLIC_IP>
- CNAME: www -> quickbusiness.co.in

Wait until:

- nslookup quickbusiness.co.in returns VPS IP
- nslookup www.quickbusiness.co.in aliases to quickbusiness.co.in

## 3) Install base software on VPS

Run as a sudo user:

sudo apt update
sudo apt -y upgrade
sudo apt -y install docker.io docker-compose-plugin nginx certbot python3-certbot-nginx git
sudo systemctl enable --now docker
sudo systemctl enable --now nginx
sudo usermod -aG docker $USER

Logout and login again once so Docker group permissions apply.

## 4) Put project on VPS

Option A: clone repo

mkdir -p /opt/retailerp
cd /opt/retailerp
git clone <YOUR_REPO_URL> .

Option B: upload current project folder to /opt/retailerp.

## 5) Configure production env

cd /opt/retailerp
cp deploy/.env.production.template .env.production

Edit .env.production and set at least:

- APP_IMAGE=retailerp:latest
- SA_PASSWORD=<strong sql password>
- JWT_SECRET=<generate with openssl rand -base64 64>
- ALLOWED_HOSTS=quickbusiness.co.in;www.quickbusiness.co.in
- ALLOW_INSECURE_COOKIES_FOR_LOCAL_HTTP=false
- FORWARDED_HEADERS_KNOWN_PROXY_0=127.0.0.1
- FORWARDED_HEADERS_KNOWN_PROXY_1=::1
- FORWARDED_HEADERS_KNOWN_PROXY_2=

Generate JWT secret:

openssl rand -base64 64

## 6) Build and start app containers

cd /opt/retailerp
docker build -t retailerp:latest .
docker compose --env-file .env.production -f docker-compose.prod.yml up -d

Check running state:

docker ps
curl -I -H "Host: quickbusiness.co.in" http://127.0.0.1:8080

Expected: HTTP 302 from app.

## 7) Configure Nginx reverse proxy

Copy prepared config from repo:

sudo cp /opt/retailerp/deploy/nginx/quickbusiness.co.in.conf /etc/nginx/sites-available/quickbusiness.co.in.conf
sudo ln -s /etc/nginx/sites-available/quickbusiness.co.in.conf /etc/nginx/sites-enabled/quickbusiness.co.in.conf
sudo nginx -t
sudo systemctl reload nginx

Now test HTTP:

curl -I http://quickbusiness.co.in

## 8) Enable HTTPS with Let\'s Encrypt

sudo certbot --nginx -d quickbusiness.co.in -d www.quickbusiness.co.in --redirect -m admin@quickbusiness.co.in --agree-tos --no-eff-email

Test HTTPS:

curl -I https://quickbusiness.co.in
curl -I https://www.quickbusiness.co.in

## 9) Post-deploy app steps

- Open https://quickbusiness.co.in/Identity/Account/Register
- Create the first owner account (becomes Admin)
- Promote this owner account to SuperAdmin (one-time DB update or admin flow)

## 10) Basic operations commands

Restart stack:

docker compose --env-file .env.production -f docker-compose.prod.yml restart

View logs:

docker compose --env-file .env.production -f docker-compose.prod.yml logs -f app

Update app after changes:

git pull
docker build -t retailerp:latest .
docker compose --env-file .env.production -f docker-compose.prod.yml up -d

## Notes

- Keep only ports 80/443 public. Do not expose 8080 publicly.
- Replace placeholder secrets before going live.
- Keep SQL and Redis volumes backed up.
