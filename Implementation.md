# Design Simplification Guidelines 

# Desired Outcomes:
    - Reduce the amount of interfaces and abstractions in the codebase, as much as possible. 
    - Reduce the amount of class fragmentation, as much as possible, due to small classes being strewn everywhere it makes the code hard to understand. 
    - Reduce the amount of abstractions that are present in the codebase, as much code as possible should be concrete and straightforward.
    - Rename elements so that it is obvious what they do, and their purpose.
    - Reduce the amount of code that is present in the codebase, as much as possible, by removing unnecessary code, and combining code where it makes sense to do so. 
    - Reduce features or odd implementation elements whenever possible to make the code base as straightforward as possible. 

# Design Changes

## Backend

### Wikipedia Ingestion: 
    
### User Prompt Ingestion: 
    - Sanitize user input and store it in the database for retrieval.
    - Be able to handle topic and link input from the user and store it in the database for retrieval, along with their session. 
    - Be able to create a new session when a new Wikipedia, topic or link is added by user. 
    - Be able to assign a uuid per session to have unique id per session. 
    - A new session should be made per use of the application. 
    - Be able to create a new user session when requested.     

### Wikipedia AI Summarizer Bot:  
    - Use Semantic Kernel With Gemini API to summarize Wikipedia articles.
    - Get Embedding from the Gemini API for a given Wikipedia article and store it in the database for retrieval.
    - Given a user prompt, retrieve relevant Wikipedia articles using the stored embeddings and the Gemini API, and summarize them for the user.
    - Given a user submitted wikipedia article, summarize it for the user using the Gemini API and find the related topics.
    - Use Wikipedia's API to retrieve the content of the article, and then use the Gemini API to summarize it.
    - Use Wikipedia's API to retrieve the content of the article, and then use the Gemini API to find related topics.
    - Use Wikipedia API data to be able to create a short description when no AI is in used.
    - Use Gemini AI to group similar Wikipedia articles together, and create a summary for the group of articles by quantifying their similarities. 

### Data Storage:
    - Store summarized user data, and sessions. 
    - Store previous user graphs, from prior sessions "mind maps".
    - Store user links that have been viewed and link it into the semantic kernel layer for retrieval.  
    - Clean up user interfaces, and adapt the data base to the new design.

### API Endpoints:
    - Create endpoint as needed to support the above functionality. 
    - Create an endpoint to retrieve user sessions and their associated data.
    - Create an endpoint to create user sessions and store user data.

