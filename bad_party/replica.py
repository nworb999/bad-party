import os
import requests
from dotenv import load_dotenv, find_dotenv

_ = load_dotenv(find_dotenv())

REPLICA_CLIENT_ID = os.environ.get("REPLICA_CLIENT_ID")
REPLICA_SECRET_KEY = os.environ.get("REPLICA_SECRET_KEY")

def do_auth():
  headers = {
    'Content-Type': 'application/x-www-form-urlencoded'
  }
  payload = f'client_id={REPLICA_CLIENT_ID}&secret={REPLICA_SECRET_KEY}'

  r = requests.post('https://api.replicastudios.com/v2/auth', headers = headers, data = payload)

  return r.json()['access_token']

token = do_auth()

headers = {
  'Authorization': f'Bearer {token}'
}

r = requests.get('https://api.replicastudios.com/speech', params={
  'txt': 'Please call Stella',  'speaker_id': 'c4fe46c4-79c0-403e-9318-ffe7bd4247dd', 'model_chain': 'classic'
}, headers = headers)

print(r.json())