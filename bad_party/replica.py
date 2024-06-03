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


def get_voice_uuid(name):
    token = do_auth()
    headers = {
    'Authorization': f'Bearer {token}'
    }

    # NATHAN !!!, the miscreant,
    r = requests.get('https://api.replicastudios.com/v2/voices', headers = headers)

    filtered_people = [person for person in r.json() if person['name'] == name]

    return filtered_people[0]['uuid']

voice_uuid = get_voice_uuid('Nathan')


# r = requests.get('https://api.replicastudios.com/speech', params={
#   'txt': 'Please call Stella',  'speaker_id': voice_uuid, 'model_chain': 'vox_1_0'
# }, headers = headers)

# print(r.json())