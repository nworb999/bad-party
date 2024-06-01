import os
from dotenv import load_dotenv, find_dotenv
from langchain.prompts import ChatPromptTemplate
from langchain_community.tools.tavily_search import TavilySearchResults
from langchain_core.messages import HumanMessage
from langchain_community.llms import Ollama
from langgraph.prebuilt import chat_agent_executor
from langgraph.checkpoint.sqlite import SqliteSaver
from ssh_tunnel import start_tunnel, stop_tunnel


_ = load_dotenv(find_dotenv())
os.environ["LANGCHAIN_TRACING_V2"] = "true"
imagination_ip = os.environ.get("IMAGINATION_IP")
imagination_port = int(os.environ.get("IMAGINATION_PORT"))
local_port = int(os.environ.get("LOCAL_PORT"))
ssh_user = os.environ.get("SSH_USERNAME")
ssh_keyfile = os.environ.get("SSH_KEYFILE")


memory = SqliteSaver.from_conn_string(":memory:")

search = TavilySearchResults(max_results=2)
tools = [search]

model = Ollama(model="llama3:70b")

start_tunnel(
    remote_server=imagination_ip,
    ssh_username=ssh_user,
    ssh_pkey=ssh_keyfile,
    remote_port=imagination_port,
    local_port=local_port,
)

agent_executor = chat_agent_executor.create_tool_calling_executor(
    model, tools, checkpointer=memory
)

template = ChatPromptTemplate.from_messages(
    [("system", "You are roleplaying as a cringey 'nice guy'.")]
)

config = {"configurable": {"thread_id": "abc123"}}

response = agent_executor.invoke(
    {
        "messages": [
            HumanMessage(
                content="with just the quote, how would you try to impress a girl you've just met.  quote: {insert quote here}"
            )
        ]
    },
    config,
)
print(response["messages"][-1].content)

stop_tunnel()
