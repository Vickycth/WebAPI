FROM ubuntu:latest

RUN apt-get update
RUN apt-get -qq update

RUN apt-get install -y curl git wget python2.7 nano ffmpeg gcc g++ make

RUN curl -sL https://deb.nodesource.com/setup_12.x | bash -
RUN apt-get install -y nodejs


# done installing node
RUN wget https://yt-dl.org/latest/youtube-dl -O /usr/local/bin/youtube-dl; chmod a+x /usr/local/bin/youtube-dl; hash -r

RUN apt-get update
EXPOSE 50052