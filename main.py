from pythonosc import udp_client
import os
from dotenv import load_dotenv
from pythonosc import osc_message_builder
from bad_party.ollama import get_response as get_response
from bad_party.ssh_tunnel import start_tunnel, stop_tunnel


load_dotenv()

imagination_ip = os.environ.get("IMAGINATION_IP")
imagination_port = int(os.environ.get("IMAGINATION_PORT"))
local_port = int(os.environ.get("LOCAL_PORT"))
ssh_user = os.environ.get("SSH_USERNAME")
ssh_keyfile = os.environ.get("SSH_KEYFILE")


def get_utterance_from_llm(prompt):
    return get_response(prompt)


def send_osc_message(client, address, dialogue):
    msg = osc_message_builder.OscMessageBuilder(address=address)
    msg.add_arg(dialogue)
    msg = msg.build()
    client.send(msg)
    print(f"Sent OSC message to {address}: {dialogue}")


def main():
    ip = "127.0.0.1"
    port = 54321

    client = udp_client.SimpleUDPClient(ip, port)

    prompt = "Start a conversation in a cringe way in one informal sentence.  No special characters."

    dialogue = get_utterance_from_llm(prompt)
    print(dialogue)
    # send_osc_message(client, "/dialogue", dialogue)


if __name__ == "__main__":
    start_tunnel(
        remote_server=imagination_ip,
        ssh_username=ssh_user,
        ssh_pkey=ssh_keyfile,
        remote_port=imagination_port,
        local_port=local_port,
    )
    main()
    stop_tunnel()
