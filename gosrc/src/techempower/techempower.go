package main

import "log"
import "net"

const s_trace = false
const s_expectedReadSize = 848
var s_responseMessage = []byte(
	"HTTP/1.1 200 OK\r\nServer: TestServer\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
	"HTTP/1.1 200 OK\r\nServer: TestServer\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
	"HTTP/1.1 200 OK\r\nServer: TestServer\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
	"HTTP/1.1 200 OK\r\nServer: TestServer\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
	"HTTP/1.1 200 OK\r\nServer: TestServer\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
	"HTTP/1.1 200 OK\r\nServer: TestServer\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
	"HTTP/1.1 200 OK\r\nServer: TestServer\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
	"HTTP/1.1 200 OK\r\nServer: TestServer\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
	"HTTP/1.1 200 OK\r\nServer: TestServer\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
	"HTTP/1.1 200 OK\r\nServer: TestServer\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
	"HTTP/1.1 200 OK\r\nServer: TestServer\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
	"HTTP/1.1 200 OK\r\nServer: TestServer\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
	"HTTP/1.1 200 OK\r\nServer: TestServer\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
	"HTTP/1.1 200 OK\r\nServer: TestServer\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
	"HTTP/1.1 200 OK\r\nServer: TestServer\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
	"HTTP/1.1 200 OK\r\nServer: TestServer\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n")

func handle_connection(c net.Conn) {
    buf := make([]byte, 4096)
    
	for {
		bytesRead, err := c.Read(buf)
		if err != nil {
			if s_trace {
				log.Printf("connection read error, err=%s", err)
			}
			c.Close()
			return
		}
        
		if bytesRead == 0 {
			if s_trace {
				log.Printf("connection closed by client")
			}
			c.Close()
			return
		}
		
		if s_trace {
	        log.Printf("read %n bytes", bytesRead)
		}
		
        if bytesRead != s_expectedReadSize {
			log.Fatal("unexpected read size %s", bytesRead)
		}
		
        bytesWritten, err := c.Write(s_responseMessage)
		if err != nil {
			if s_trace {
				log.Printf("connection write error, err=%s", err)
			}
			c.Close()
			return
		}
        
		if s_trace {
	        log.Printf("wrote %n bytes", bytesWritten)
		}
	}
}

func main() {
	// Listen on TCP port 5000 on all interfaces.
	listener, err := net.Listen("tcp", ":5000")
	if err != nil {
		log.Fatal(err)
	}
	defer listener.Close()
    
    log.Printf("Server running")
    
    for {
		// Wait for a connection.
		conn, err := listener.Accept()
		if err != nil {
			log.Fatal(err)
		}
        
		if s_trace {
	        log.Printf("Connection accepted")
		}
        
        go handle_connection(conn)
    }
}