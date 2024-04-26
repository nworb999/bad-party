from pythonosc import udp_client
from pythonosc import osc_message_builder


def get_utterance_from_llm(prompt):
    return "Hello, this is a response from the LLM based on the prompt: " + prompt


def send_osc_message(client, address, dialogue):
    msg = osc_message_builder.OscMessageBuilder(address=address)
    msg.add_arg(dialogue)
    msg = msg.build()
    client.send(msg)
    print(f"Sent OSC message to {address}: {dialogue}")


def main():
    ip = "127.0.0.1"
    port = 12345

    client = udp_client.SimpleUDPClient(ip, port)

    prompt = "Start off a conversation in an unsettling way."

    dialogue = get_utterance_from_llm(prompt)

    send_osc_message(client, "/dialogue", dialogue)


if __name__ == "__main__":
    main()
