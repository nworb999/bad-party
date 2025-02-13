import socket
import random
import json

HOST = '127.0.0.1'
PORT = 8001

def generate_random_thought():
    thoughts = [
        "I'm thinking...",
        "What if...",
        "Why not...",
        "Let's try...",
        "How about...",
    ]
    return random.choice(thoughts)

def handle_state_change(event_data):
    return generate_random_thought()

event_handlers = {
    "state_change": handle_state_change,
}

with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
    s.bind((HOST, PORT))
    s.listen()
    conn, addr = s.accept()
    with conn:
        print('Connected by', addr)
        while True:
            data = conn.recv(1024)
            if not data:
                break
            try:
                json_data = json.loads(data.decode('utf-8'))
                event_type = json_data['event_type']
                agent_id = json_data['agent_id']
                event_data = json_data['data']

                print('Received', event_type, agent_id, event_data)

                if event_type in event_handlers:
                    response = event_handlers[event_type](event_data)
                else:
                    response = "Unknown event type"

                conn.sendall(response.encode('utf-8'))

            except json.JSONDecodeError:
                print("Received invalid JSON data")
                conn.sendall("Invalid JSON data".encode('utf-8'))
            except Exception as e:
                print(f"Error processing data: {e}")
                conn.sendall(f"Error: {e}".encode('utf-8')) 