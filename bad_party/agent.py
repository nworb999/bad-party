import os
import dspy
from dotenv import load_dotenv, find_dotenv
from ssh_tunnel import start_tunnel, stop_tunnel


_ = load_dotenv(find_dotenv())

imagination_ip = os.environ.get("IMAGINATION_IP")
imagination_port = int(os.environ.get("IMAGINATION_PORT"))
local_port = int(os.environ.get("LOCAL_PORT"))
ssh_user = os.environ.get("SSH_USERNAME")
ssh_keyfile = os.environ.get("SSH_KEYFILE")


start_tunnel(
    remote_server=imagination_ip,
    ssh_username=ssh_user,
    ssh_pkey=ssh_keyfile,
    remote_port=imagination_port,
    local_port=local_port,
)


ollama_model = dspy.OpenAI(api_base=f"http://localhost:{local_port}/", api_key='ollama', model='llama3:70b', stop='\n\n', model_type='chat')

dspy.settings.configure(lm=ollama_model)

my_example = {
    "question": "Come up with a short workplace story from the perspective of a machine trying to pass as a nice human"
}

class BasicQA(dspy.Signature):
    question = dspy.InputField(desc="Details for a story")
    answer = dspy.OutputField(desc="The story")

# define the predictor
generate_answer = dspy.Predict(BasicQA)

pred = generate_answer(question=my_example['question'])

print(pred.answer)

stop_tunnel()
